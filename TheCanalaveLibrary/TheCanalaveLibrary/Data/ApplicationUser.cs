using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace TheCanalaveLibrary.Data;

// Add profile data for application users by adding properties to the ApplicationUser class
public class ApplicationUser : IdentityUser<int>
{
    [StringLength(500)]
    public string? ProfilePictureUrl { get; set; }
    [StringLength(256)]
    public string? Tagline { get; set; }
    public string? ProfileText { get; set; }
    
    // 'Role' is handled by Identity Roles, so you can remove it.
    
    public bool ShowMatureContent { get; set; } = false;
    [StringLength(50)]
    public string? ThemeName { get; set; }
    public bool PrefersDataSaverMode { get; set; } = false;
    public bool PrefersAnimatedSprites { get; set; } = true;
    
    // 'PasswordHash', 'PasswordSalt', 'Email', 'Username' are already 
    // part of the base IdentityUser.
}