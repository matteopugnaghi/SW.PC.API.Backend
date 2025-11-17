using System.ComponentModel.DataAnnotations;

namespace SW.PC.API.Backend.Models
{
    public class Model3D
    {
        public string Id { get; set; } = string.Empty;
        
        [Required]
        public string Name { get; set; } = string.Empty;
        
        public string? Description { get; set; }
        
        [Required]
        public string FileName { get; set; } = string.Empty;
        
        public string FileType { get; set; } = string.Empty; // glb, gltf, obj, stl
        
        public long FileSizeBytes { get; set; }
        
        public string? ThumbnailUrl { get; set; }
        
        public Dictionary<string, object>? Metadata { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}