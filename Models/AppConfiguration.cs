namespace SW.PC.API.Backend.Models
{
    public class AppConfiguration
    {
        public ColorConfiguration Colors { get; set; } = new ColorConfiguration();
        
        public ViewerConfiguration Viewer { get; set; } = new ViewerConfiguration();
        
        public Dictionary<string, object> Custom { get; set; } = new Dictionary<string, object>();
    }
    
    public class ColorConfiguration
    {
        public string DefaultColor { get; set; } = "#FF0000";
        
        public string[] AvailableColors { get; set; } = new[]
        {
            "#FF0000", "#00FF00", "#0000FF", "#FFFF00", 
            "#FF00FF", "#00FFFF", "#FFA500", "#800080"
        };
        
        public string ColorPolicy { get; set; } = "override"; // tint, override
        
        public bool EnableColorPanel { get; set; } = true;
    }
    
    public class ViewerConfiguration
    {
        public CameraConfiguration Camera { get; set; } = new CameraConfiguration();
        
        public LightingConfiguration Lighting { get; set; } = new LightingConfiguration();
        
        public bool EnableGridHelper { get; set; } = true;
        
        public bool EnableAxesHelper { get; set; } = false;
    }
    
    public class CameraConfiguration
    {
        public float[] Position { get; set; } = new[] { 5.0f, 5.0f, 5.0f };
        
        public float[] Target { get; set; } = new[] { 0.0f, 0.0f, 0.0f };
        
        public float Fov { get; set; } = 75.0f;
        
        public float Near { get; set; } = 0.1f;
        
        public float Far { get; set; } = 1000.0f;
    }
    
    public class LightingConfiguration
    {
        public AmbientLightConfiguration Ambient { get; set; } = new AmbientLightConfiguration();
        
        public DirectionalLightConfiguration Directional { get; set; } = new DirectionalLightConfiguration();
        
        public float EnvironmentIntensity { get; set; } = 1.0f;
    }
    
    public class AmbientLightConfiguration
    {
        public string Color { get; set; } = "#404040";
        
        public float Intensity { get; set; } = 0.4f;
    }
    
    public class DirectionalLightConfiguration
    {
        public string Color { get; set; } = "#ffffff";
        
        public float Intensity { get; set; } = 0.8f;
        
        public float[] Position { get; set; } = new[] { 10.0f, 10.0f, 5.0f };
    }
}