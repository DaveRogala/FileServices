using System.Diagnostics.CodeAnalysis;

namespace MagellanFileServices.Models
{
    public class ObjectResult<T>
    {
        [SetsRequiredMembers]
        public ObjectResult()
        {            
            Errors = [];
        }
        [SetsRequiredMembers]
        public ObjectResult(List<T>? objectResults, List<string> errors)
        {
            ObjectResults = objectResults;
            Errors = errors;
            
        }
        public List<T>? ObjectResults { get; set; }
        public required List<string> Errors { get; set; }
        public bool CriticalError { get; set; } = false;
    }
}
