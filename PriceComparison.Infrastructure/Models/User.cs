using System;
using System.Collections.Generic;

namespace PriceComparison.Infrastructure.Models;

public partial class User
{
    public int Id { get; set; }

    /// <summary>
    /// מספר טלפון ישראלי (אופציונלי אבל אחד מטלפון/אימייל חובה)
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// כתובת אימייל (אופציונלי אבל אחד מטלפון/אימייל חובה)
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// שם מלא של המשתמש
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// סיסמה מוצפנת עם BCrypt
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// האם המשתמש פעיל במערכת
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// תאריך יצירת המשתמש
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    /// <summary>
    /// תאריך והשעה של התחברות אחרונה
    /// </summary>
    public DateTime? LastLogin { get; set; }

    /// <summary>
    /// מחזיר את מזהה הכניסה (טלפון או אימייל)
    /// </summary>
    public string GetLoginIdentifier()
    {
        return !string.IsNullOrEmpty(Phone) ? Phone : Email ?? "";
    }
}