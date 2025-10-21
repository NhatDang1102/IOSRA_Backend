using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Contract.DTOs.Request.Admin
{
    /// <summary>
    /// Tham số lọc/phân trang cho danh sách tài khoản
    /// </summary>
    public class AccountQuery
    {
        // Chuỗi tìm kiếm theo username/email
        [MaxLength(100, ErrorMessage = "Search tối đa 100 ký tự.")]
        public string? Search { get; set; }

        // unbanned | banned (để trống = tất cả)
        [RegularExpression("^(unbanned|banned)?$", ErrorMessage = "Status phải là 'unbanned' hoặc 'banned'.")]
        public string? Status { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Page phải >= 1")]
        public int Page { get; set; } = 1;

        [Range(1, 200, ErrorMessage = "PageSize nằm trong [1..200]")]
        public int PageSize { get; set; } = 20;
    }
}
