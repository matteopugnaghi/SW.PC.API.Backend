using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Models.Database;

namespace SW.PC.API.Backend.Data
{
    public class ScadaDbContext : DbContext
    {
        public ScadaDbContext(DbContextOptions<ScadaDbContext> options) : base(options)
        {
        }
        
        // DbSets para las tablas
        public DbSet<Alarm> Alarms { get; set; }
        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<RecipeStep> RecipeSteps { get; set; }
        public DbSet<ProductionStatistic> ProductionStatistics { get; set; }
        public DbSet<SystemLog> SystemLogs { get; set; }
        public DbSet<ScadaUser> ScadaUsers { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Configuración de Alarm
            modelBuilder.Entity<Alarm>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.AlarmCode);
                entity.HasIndex(e => e.TriggeredAt);
                entity.HasIndex(e => e.Status);
                
                entity.Property(e => e.AdditionalData)
                    .HasColumnType("nvarchar(max)")
                    .HasConversion(
                        v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                        v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null)!
                    );
            });
            
            // Configuración de Recipe
            modelBuilder.Entity<Recipe>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.ProductType);
                
                entity.Property(e => e.Parameters)
                    .HasColumnType("nvarchar(max)")
                    .HasConversion(
                        v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                        v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null)!
                    );
                
                entity.HasMany(e => e.Steps)
                    .WithOne(e => e.Recipe)
                    .HasForeignKey(e => e.RecipeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            
            // Configuración de RecipeStep
            modelBuilder.Entity<RecipeStep>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.RecipeId);
                
                entity.Property(e => e.Parameters)
                    .HasColumnType("nvarchar(max)")
                    .HasConversion(
                        v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                        v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null)!
                    );
                
                entity.Property(e => e.Conditions)
                    .HasColumnType("nvarchar(max)")
                    .HasConversion(
                        v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                        v => v == null ? null : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null)
                    );
            });
            
            // Configuración de ProductionStatistic
            modelBuilder.Entity<ProductionStatistic>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.MachineId);
                
                entity.Property(e => e.CustomMetrics)
                    .HasColumnType("nvarchar(max)")
                    .HasConversion(
                        v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                        v => v == null ? null : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null)
                    );
            });
            
            // Configuración de SystemLog
            modelBuilder.Entity<SystemLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.Level);
                entity.HasIndex(e => e.Source);
            });
            
            // Configuración de ScadaUser
            modelBuilder.Entity<ScadaUser>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email);
                
                entity.Property(e => e.Permissions)
                    .HasColumnType("nvarchar(max)")
                    .HasConversion(
                        v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                        v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null)!
                    );
            });
            
            // Datos iniciales
            SeedData(modelBuilder);
        }
        
        private void SeedData(ModelBuilder modelBuilder)
        {
            // Usuario administrador por defecto
            modelBuilder.Entity<ScadaUser>().HasData(
                new ScadaUser
                {
                    Id = 1,
                    Username = "admin",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"), // Cambiar en producción
                    FullName = "Administrator",
                    Email = "admin@scada.local",
                    Role = UserRole.Administrator,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    Permissions = new List<string> { "all" }
                }
            );
        }
    }
}