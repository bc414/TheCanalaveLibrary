-- Messaging volume seed for the three `messaging_inbox_*` PerfBaseline scenarios.
--
-- WHY THIS EXISTS SEPARATELY FROM SeedTool: SeedTool generates no conversations or messages, which
-- is exactly why F49's L6 cells were flipped to Stage 5 unmeasured (.claude/design/L6-reconciliation-matrix.md
-- §Messaging). Adding a messaging generator to SeedTool is tracked separately (C4's messaging half);
-- this script is the minimum needed to make the WU-MsgReadPath measurement reproducible without
-- pre-empting that work.
--
--   psql -h localhost -U postgres -d TheCanalaveLibraryDB -f TheCanalaveLibrary.PerfBaseline/seed-messaging-volume.sql
--   dotnet run --project TheCanalaveLibrary.PerfBaseline -- --label <name>
--   psql ... -c "DELETE FROM conversations WHERE subject LIKE 'PERFSEED %';"   -- cleanup (cascades)
--
-- Shape: 400 conversations for user 1 (the DataSeeder TestUser), 3-40 messages each, bodies 1-6 KB
-- (~28 MB total). The fat bodies are the point — they are what makes the preview-substring and the
-- top-1-per-conversation access path measurable. Partners rotate over users 3..7.
--
-- NOT idempotent: re-running adds another 400. Delete first (see cleanup above).

INSERT INTO conversations (subject, date_created)
SELECT 'PERFSEED conversation ' || g,
       TIMESTAMPTZ '2026-01-01 00:00:00+00' + (g || ' minutes')::interval
FROM generate_series(1, 400) g;

INSERT INTO conversation_participants (conversation_id, user_id, is_archived, last_read_timestamp)
SELECT c.conversation_id, 1, false, TIMESTAMPTZ '2026-02-01 00:00:00+00'
FROM conversations c WHERE c.subject LIKE 'PERFSEED %';

INSERT INTO conversation_participants (conversation_id, user_id, is_archived, last_read_timestamp)
SELECT c.conversation_id, 3 + (c.conversation_id % 5), false, NULL
FROM conversations c WHERE c.subject LIKE 'PERFSEED %';

INSERT INTO private_messages (conversation_id, sender_user_id, message_text, date_sent)
SELECT c.conversation_id,
       CASE WHEN m % 2 = 0 THEN 1 ELSE 3 + (c.conversation_id % 5) END,
       '<p>Message ' || m || ' in conversation ' || c.conversation_id || '. '
         || repeat('Some reasonably wordy fanfiction discussion text. ',
                   20 + (c.conversation_id % 100))
         || '</p>',
       TIMESTAMPTZ '2026-01-02 00:00:00+00'
         + ((c.conversation_id * 37 + m) || ' minutes')::interval
FROM conversations c
CROSS JOIN LATERAL generate_series(1, 3 + (c.conversation_id % 38)) m
WHERE c.subject LIKE 'PERFSEED %';

ANALYZE conversations;
ANALYZE conversation_participants;
ANALYZE private_messages;

SELECT (SELECT count(*) FROM conversations WHERE subject LIKE 'PERFSEED %') AS convs,
       (SELECT count(*) FROM private_messages m JOIN conversations c
          ON c.conversation_id = m.conversation_id WHERE c.subject LIKE 'PERFSEED %') AS msgs;
