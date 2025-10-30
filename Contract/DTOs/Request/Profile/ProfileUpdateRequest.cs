using System;
using System.ComponentModel.DataAnnotations;

namespace Contract.DTOs.Request.Profile
{
    public class ProfileUpdateRequest
    {
        [StringLength(500, ErrorMessage = "Bio must not exceed 500 characters.")]
        public string? Bio { get; set; }

        [RegularExpression(@"^(M|F|other|unspecified)$", ErrorMessage = "Gender must be one of M, F, other, or unspecified.")]
        public string? Gender { get; set; }

        [DataType(DataType.Date, ErrorMessage = "Birthday must be a valid date (yyyy-MM-dd).")]
        public DateOnly? Birthday { get; set; }
    }
}
