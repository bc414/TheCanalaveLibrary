using System.Text;
using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Site-level crawlability surface (Feature 64, WU-AccessGate; audit/Seo.md).
///
/// <b>robots.txt</b> — served as an endpoint (not a wwwroot static file) because the
/// <c>Sitemap:</c> line must be an ABSOLUTE url per the robots spec, and the one sanctioned
/// absolute-URL seam is <see cref="IPublicUrlProvider"/> (never request-derived — Cloudflare
/// proxy rationale in audit/Seo.md). Search crawlers are welcome; named AI-training crawlers are
/// disallowed (settled 2026-07-19 — matches the AO3/FFN/Fimfiction class norm and fanfic-author
/// expectations). /api, /Account, /content-gate, and /status-code are excluded from crawling for
/// everyone — no crawler has business in JSON, auth flows, consent POST targets, or re-execute
/// targets.
///
/// <b>sitemap.xml</b> — every publicly-visible published story, INCLUDING M-rated (index-all,
/// decision row 11: M pages are gated, never de-listed; the interstitial is the indexable
/// artifact). Elevated read: ContentRating bypassed, IsTakenDown kept. Single sitemap file —
/// the 50k-URL spec cap is orders of magnitude away; revisit with an index file if ever near.
///
/// <b>Canonical-slug 301</b> — the spec'd-but-never-built redirect (addendum #16): a story URL
/// with a stale/wrong slug segment 301s to the canonical slug. Implemented as middleware (full
/// documents only — SPA navs never hit the server per-nav) so the redirect is a true 301 before
/// any rendering; the slug lookup is a PK-indexed scalar read. Slug comparison is
/// case-sensitive-exact: anything not exactly canonical redirects once.
/// </summary>
public static class SeoEndpoints
{
    public static WebApplication MapSeoEndpoints(this WebApplication app)
    {
        app.MapGet("/robots.txt", (IPublicUrlProvider urls) =>
        {
            string sitemapUrl = urls.AbsolutePageUrl("/sitemap.xml");
            string body = $"""
                # The Canalave Library — search indexing welcome; AI-training crawlers are not.
                User-agent: *
                Allow: /
                Disallow: /api/
                Disallow: /Account/
                Disallow: /content-gate/
                Disallow: /status-code/

                User-agent: GPTBot
                Disallow: /

                User-agent: CCBot
                Disallow: /

                User-agent: ClaudeBot
                Disallow: /

                User-agent: Google-Extended
                Disallow: /

                User-agent: Applebot-Extended
                Disallow: /

                User-agent: Bytespider
                Disallow: /

                User-agent: Amazonbot
                Disallow: /

                User-agent: meta-externalagent
                Disallow: /

                Sitemap: {sitemapUrl}
                """;
            return Results.Text(body, "text/plain", Encoding.UTF8);
        });

        app.MapGet("/sitemap.xml", async (
            IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
            IPublicUrlProvider urls) =>
        {
            await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

            // elevated read: index-all — M stories belong in the sitemap (their gated
            // interstitial is the indexable page). IsTakenDown stays active, and the
            // "StoryStatus" filter (anonymous scope → public statuses only) excludes
            // Draft/PendingApproval/Rejected without an explicit predicate (WU-AccessGate2).
            var rows = await readDb.Stories
                .IgnoreQueryFilters(["ContentRating"])
                .OrderBy(s => s.StoryId)
                .Select(s => new
                {
                    s.StoryId,
                    Slug = s.StoryDetail != null ? s.StoryDetail.Slug : null,
                    s.LastUpdatedDate,
                })
                .ToListAsync();

            // The other three "original content home" types (settled 2026-07-23: stories +
            // Public-visibility profiles + groups + published blog posts are IN; chapters ride
            // story-page links, series/custom lists are rearrangements, tag/search pages are
            // navigation — all OUT).

            // Profiles: Public visibility only, and deliberately NO <lastmod> — deriving one
            // from activity would leak activity for users who hid their status.
            List<int> profileIds = await readDb.Users
                .Where(u => u.PrivacySettings.ProfileVisibility == ProfileVisibility.Public)
                .OrderBy(u => u.Id)
                .Select(u => u.Id)
                .ToListAsync();

            // Groups: all, including M-audience (index-all — the gated page is indexable).
            var groupRows = await readDb.Groups
                .IgnoreQueryFilters(["GroupAudience"])
                .OrderBy(g => g.GroupId)
                .Select(g => new { g.GroupId, g.DateCreated })
                .ToListAsync();

            // Blog posts: both TPT subtypes, published only; takedown filter active; the
            // group-audience gate bypassed for group posts (their gated page is indexable).
            var profilePostRows = await readDb.ProfileBlogPosts
                .Where(p => p.IsPublished)
                .OrderBy(p => p.BlogPostId)
                .Select(p => new { p.BlogPostId, p.LastUpdatedDate })
                .ToListAsync();
            var groupPostRows = await readDb.GroupBlogPosts
                .IgnoreQueryFilters(["GroupAudience"])
                .Where(p => p.IsPublished)
                .OrderBy(p => p.BlogPostId)
                .Select(p => new { p.BlogPostId, p.LastUpdatedDate })
                .ToListAsync();

            var xml = new StringBuilder();
            xml.Append("""<?xml version="1.0" encoding="UTF-8"?>""").Append('\n');
            xml.Append("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""").Append('\n');

            void AppendUrl(string path, DateTime? lastmod)
            {
                xml.Append("  <url><loc>")
                   .Append(System.Security.SecurityElement.Escape(urls.AbsolutePageUrl(path)));
                xml.Append("</loc>");
                if (lastmod is DateTime lm)
                    xml.Append("<lastmod>").Append(lm.ToString("yyyy-MM-dd")).Append("</lastmod>");
                xml.Append("</url>\n");
            }

            foreach (var row in rows)
            {
                string path = string.IsNullOrEmpty(row.Slug)
                    ? $"/story/{row.StoryId}"
                    : $"/story/{row.StoryId}/{row.Slug}";
                AppendUrl(path, row.LastUpdatedDate);
            }
            foreach (int id in profileIds) AppendUrl($"/user/{id}", lastmod: null);
            foreach (var g in groupRows) AppendUrl($"/group/{g.GroupId}", g.DateCreated);
            foreach (var p in profilePostRows) AppendUrl($"/blog/{p.BlogPostId}", p.LastUpdatedDate);
            foreach (var p in groupPostRows) AppendUrl($"/blog/{p.BlogPostId}", p.LastUpdatedDate);

            // WU-TagFanon 6.6: the public fanon pages join the sitemap (community reference
            // surfaces; the personal /tag-adoptions plane stays out).
            AppendUrl("/fanon", lastmod: null);
            AppendUrl("/fanon/characters", lastmod: null);
            AppendUrl("/fanon/settings", lastmod: null);

            xml.Append("</urlset>\n");

            return Results.Text(xml.ToString(), "application/xml", Encoding.UTF8);
        });

        return app;
    }

    /// <summary>The canonical-slug 301 middleware — see the type doc comment.</summary>
    public static IApplicationBuilder UseCanonicalStorySlugRedirect(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            // Only full-document GET/HEAD navigations to /story/{id}/{slug} are candidates.
            // /story/{id} (no slug) and /story/{id}/{chapter:int} (reading page) are left alone —
            // the int-constrained chapter routes shadow numeric second segments by design.
            if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
                && TryParseStorySlugPath(context.Request.Path, out int storyId, out string requestSlug))
            {
                using IServiceScope scope = context.RequestServices.CreateScope();
                var readDbFactory = scope.ServiceProvider
                    .GetRequiredService<IDbContextFactory<ReadOnlyApplicationDbContext>>();
                await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

                // Slug is Class-B metadata — the canonical URL is public (it's in the sitemap),
                // so the lookup ignores the rating filter; takedown stays a 404 concern for the
                // page itself (no redirect for a story the takedown filter hides).
                string? canonicalSlug = await readDb.Stories
                    .IgnoreQueryFilters(["ContentRating"])
                    .Where(s => s.StoryId == storyId)
                    .Select(s => s.StoryDetail != null ? s.StoryDetail.Slug : null)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrEmpty(canonicalSlug)
                    && !string.Equals(requestSlug, canonicalSlug, StringComparison.Ordinal))
                {
                    string target = $"/story/{storyId}/{Uri.EscapeDataString(canonicalSlug)}";
                    if (context.Request.QueryString.HasValue)
                        target += context.Request.QueryString.Value;
                    context.Response.Redirect(target, permanent: true);
                    return;
                }
            }

            await next(context);
        });

    private static bool TryParseStorySlugPath(PathString path, out int storyId, out string slug)
    {
        storyId = 0;
        slug = string.Empty;
        if (!path.HasValue) return false;

        string[] segments = path.Value!.Trim('/').Split('/');
        if (segments.Length != 3 || !segments[0].Equals("story", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!int.TryParse(segments[1], out storyId)) return false;

        slug = Uri.UnescapeDataString(segments[2]);
        // Numeric third segment = the chapter reading route, never a slug.
        return !int.TryParse(slug, out _);
    }
}
