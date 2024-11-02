using System.ComponentModel.DataAnnotations.Schema;

namespace AutoGenerateContent.Models
{
    public class Config
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? SearchText { get; set; }
        public string? PromptText {  get; set; }
        public string? PromptComplete {  get; set; }
        public string? SearchImageText { get; set; }

        [NotMapped]
        public string? Description { get; set; }
    }
}
