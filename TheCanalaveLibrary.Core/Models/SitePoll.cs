namespace TheCanalaveLibrary.Core.Models;

public class SitePoll : BasePoll
{
    //Created by moderators only, appears on front page or on in a separate page
    
    //If the admins want to retire it on the display screen
    public bool IsArchived { get; set; }
}