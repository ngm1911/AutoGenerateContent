using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoGenerateContent.Models
{
    public class Config
    {
        public int Id { get; set; }
        public string Name { get; set; }

        [NotMapped]
        public string Description {
            get
            {
                if (Name == "Create New Config")
                {
                   return "";
                }
                return Name;
            }
            set
            {
                Name = value;
            }
        }
        public string SearchText { get; set; }
        public string PromptText {  get; set; }
        public string PromptComplete {  get; set; }
        public string SearchImageText { get; set; }
    }
}
