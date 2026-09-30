using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Data;

public static class DbUpdateErrors
{
    // Қайталанған feedback не summary жазбасының unique индекс қатесін ажыратады.
    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    // Қатар орындалған жазулардың unique және foreign key қателерін таниды.
    public static bool IsConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 or 547 };
}
