using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Request.Profile
{
    public class ProfileUpdateRequest
    {
        [StringLength(500, ErrorMessage = "Bio tối đa 500 ký tự")]
        public string? Bio { get; set; }

        [RegularExpression(@"^(M|F|other|unspecified)$", ErrorMessage = "Gender chỉ nhận M/F/other/unspecified")]
        public string? Gender { get; set; }

        [DataType(DataType.Date, ErrorMessage = "Birthday phải là ngày (yyyy-MM-dd)")]
        public DateOnly? Birthday { get; set; }
    }
}
