using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Data;

public static class DbUpdateErrors
{
    // Pre-checks give normal validation messages; this covers concurrent writes.
    public static bool IsConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 or 547 };
}
