using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace WebApplication1.Models
{
    public class HomeViewModel
    {
        [Display(Name = "Search Term")]
        [Required(ErrorMessage = "A Search Term is required.")]
        [StringLength(30, MinimumLength = 3, ErrorMessage = "Search Term too short.")]
        public string SearchTerm { get; set; }


        public List<LuceneSearch.SearchResult> SearchResults { get; set; }
    }
}