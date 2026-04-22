using System.Diagnostics.CodeAnalysis;

namespace MagellanFileServices.Models
{
    public class FileWriteResult
    {
        [SetsRequiredMembers]
        public FileWriteResult(List<string> errors)
        {
            Errors = errors;
        }
        [SetsRequiredMembers]
        public FileWriteResult()
        {
            Errors = [];
        }
        public required List<string> Errors { get; set; }
        
    }
}
