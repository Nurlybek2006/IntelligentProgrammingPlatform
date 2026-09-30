using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Data;

public static class DbUpdateErrors
{
    // Қатар орындалған жазулардың unique және foreign key қателерін таниды.
    public static bool IsConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 or 547 };
}
