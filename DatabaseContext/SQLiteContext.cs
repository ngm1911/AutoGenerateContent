using AutoGenerateContent.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Serilog;

namespace AutoGenerateContent.DatabaseContext
{
    public class SQLiteContext(DbContextOptions<SQLiteContext> options) : DbContext(options)
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                          .LogTo(x => Log.Logger.Debug(x),
                                    events:
                                    [
                                        RelationalEventId.CommandExecuted,
                                    ])
                          .EnableDetailedErrors()
                          .EnableSensitiveDataLogging();
        }
        public DbSet<Config> configs { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //Global filter here
            //modelBuilder.Entity<Table>().HasQueryFilter(b => b.IsDeleted = "1");
            modelBuilder.Entity<Config>().HasKey(x => x.Id);
            modelBuilder.Entity<Config>().Property(x => x.Id).ValueGeneratedOnAdd();

            base.OnModelCreating(modelBuilder);
        }
    }
}
