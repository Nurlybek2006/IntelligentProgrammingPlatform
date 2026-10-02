using IntelligentProgrammingPlatform.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        // EF Core контекстіне SQL Server қосылым баптауын береді.
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Topic> Topics { get; set; }
        public DbSet<Lesson> Lessons { get; set; }
        public DbSet<ProgrammingTask> ProgrammingTasks { get; set; }
        public DbSet<TestCase> TestCases { get; set; }
        public DbSet<Runtime> Runtimes { get; set; }
        public DbSet<Submission> Submissions { get; set; }
        public DbSet<ExecutionResult> ExecutionResults { get; set; }
        public DbSet<Leaderboard> Leaderboards { get; set; }
        public DbSet<AiFeedback> AiFeedbacks { get; set; }

        // Identity мен домен кестелерінің байланыстарын және шектеулерін баптайды.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(user => user.DisplayName).HasMaxLength(100).IsRequired();
            });

            modelBuilder.Entity<Topic>(entity =>
            {
                entity.Property(topic => topic.Name).HasMaxLength(100).IsRequired();
                entity.Property(topic => topic.Description).HasMaxLength(2000);
                entity.HasIndex(topic => topic.Name).IsUnique();
            });

            modelBuilder.Entity<Lesson>(entity =>
            {
                entity.Property(lesson => lesson.Title).HasMaxLength(200).IsRequired();
                entity.Property(lesson => lesson.Slug).HasMaxLength(200).IsRequired();
                entity.Property(lesson => lesson.Summary).HasMaxLength(1000).IsRequired();
                entity.Property(lesson => lesson.Content).HasMaxLength(50000).IsRequired();
                entity.Property(lesson => lesson.CodeExample).HasMaxLength(16000);
                entity.Property(lesson => lesson.CodeLanguage).HasMaxLength(20);
                entity.HasIndex(lesson => lesson.Slug).IsUnique();
                entity.HasIndex(lesson => new { lesson.TopicId, lesson.Order }).IsUnique();
                entity.ToTable(table => table.HasCheckConstraint("CK_Lessons_Order", "[Order] > 0"));
                entity.HasOne(lesson => lesson.Topic).WithMany(topic => topic.Lessons)
                    .HasForeignKey(lesson => lesson.TopicId).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<ProgrammingTask>(entity =>
            {
                entity.Property(task => task.Title).HasMaxLength(200).IsRequired();
                entity.Property(task => task.Slug).HasMaxLength(200).IsRequired();
                entity.Property(task => task.Description).IsRequired();
                entity.HasIndex(task => task.Slug).IsUnique();

                entity.HasOne(task => task.Topic)
                    .WithMany(topic => topic.ProgrammingTasks)
                    .HasForeignKey(task => task.TopicId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<TestCase>(entity =>
            {
                // Empty input/output is valid; neither value should be NULL.
                entity.Property(testCase => testCase.Input).IsRequired();
                entity.Property(testCase => testCase.ExpectedOutput).IsRequired();
                entity.HasIndex(testCase => new { testCase.ProgrammingTaskId, testCase.Order })
                    .IsUnique();

                entity.HasOne(testCase => testCase.ProgrammingTask)
                    .WithMany(task => task.TestCases)
                    .HasForeignKey(testCase => testCase.ProgrammingTaskId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<Runtime>(entity =>
            {
                entity.Property(runtime => runtime.Name).HasMaxLength(100).IsRequired();
                entity.Property(runtime => runtime.LanguageKey).HasMaxLength(50).IsRequired();
                entity.Property(runtime => runtime.Version).HasMaxLength(50).IsRequired();
                entity.Property(runtime => runtime.FileExtension).HasMaxLength(20).IsRequired();
                entity.Property(runtime => runtime.CompileCommand).HasMaxLength(2000);
                entity.Property(runtime => runtime.RunCommand).HasMaxLength(2000).IsRequired();
                entity.Property(runtime => runtime.DockerImage).HasMaxLength(500);
                entity.HasIndex(runtime => runtime.LanguageKey).IsUnique();
            });

            modelBuilder.Entity<Submission>(entity =>
            {
                entity.Property(submission => submission.UserId).HasMaxLength(450).IsRequired();
                entity.Property(submission => submission.SourceCode).IsRequired();
                entity.HasIndex(submission => submission.UserId);
                entity.HasIndex(submission => submission.ProgrammingTaskId);
                entity.HasIndex(submission => submission.CreatedAt);
                entity.HasIndex(submission => submission.Status);

                // Preserve submission history when a parent is deleted.
                entity.HasOne(submission => submission.User)
                    .WithMany(user => user.Submissions)
                    .HasForeignKey(submission => submission.UserId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(submission => submission.ProgrammingTask)
                    .WithMany(task => task.Submissions)
                    .HasForeignKey(submission => submission.ProgrammingTaskId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(submission => submission.Runtime)
                    .WithMany(runtime => runtime.Submissions)
                    .HasForeignKey(submission => submission.RuntimeId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<ExecutionResult>(entity =>
            {
                // A submission has at most one result for each test case.
                entity.HasIndex(result => new { result.SubmissionId, result.TestCaseId })
                    .IsUnique();

                entity.HasOne(result => result.Submission)
                    .WithMany(submission => submission.ExecutionResults)
                    .HasForeignKey(result => result.SubmissionId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(result => result.TestCase)
                    .WithMany(testCase => testCase.ExecutionResults)
                    .HasForeignKey(result => result.TestCaseId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<AiFeedback>(entity =>
            {
                entity.Property(feedback => feedback.UserId).HasMaxLength(450).IsRequired();
                entity.Property(feedback => feedback.Model).HasMaxLength(100).IsRequired();
                entity.Property(feedback => feedback.Summary).HasMaxLength(600).IsRequired();
                entity.Property(feedback => feedback.Explanation).HasMaxLength(3000).IsRequired();
                entity.Property(feedback => feedback.HintsJson).HasMaxLength(6000).IsRequired();
                entity.Property(feedback => feedback.RevealedHintCount).HasDefaultValue(1).HasSentinel(-1);
                entity.ToTable(table => table.HasCheckConstraint("CK_AiFeedbacks_RevealedHintCount",
                    "[RevealedHintCount] BETWEEN 0 AND 3"));
                entity.Property(feedback => feedback.ErrorCategory).HasMaxLength(30).IsRequired();
                entity.HasIndex(feedback => feedback.SubmissionId).IsUnique();
                entity.HasOne(feedback => feedback.Submission)
                    .WithOne(submission => submission.AiFeedback)
                    .HasForeignKey<AiFeedback>(feedback => feedback.SubmissionId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(feedback => feedback.User).WithMany()
                    .HasForeignKey(feedback => feedback.UserId).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<Leaderboard>(entity =>
            {
                entity.Property(leaderboard => leaderboard.UserId).HasMaxLength(450).IsRequired();
                entity.HasIndex(leaderboard => leaderboard.UserId).IsUnique();

                entity.HasOne(leaderboard => leaderboard.User)
                    .WithOne(user => user.Leaderboard)
                    .HasForeignKey<Leaderboard>(leaderboard => leaderboard.UserId)
                    .OnDelete(DeleteBehavior.NoAction);
            });
        }
    }
}
