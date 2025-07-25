using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceComparison.Download.Models
{
    /// <summary>
    /// פרטי התחברות לרשת
    /// </summary>
    public class LoginCredentials
    {
        /// <summary>
        /// שם משתמש
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// סיסמה
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// האם נדרשת סיסמה
        /// </summary>
        public bool RequiresPassword { get; set; } = false;
    }
}
