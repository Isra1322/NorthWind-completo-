using NorthWind.Exceptions.Entities.Exceptions;

namespace NorthWind.Sales.Backend.DataContexts.EFCore.Guards;

internal static class GuardDBContext
{
    public static async Task AgainstSaveChangesErrorAsync(DbContext context)
    {
        try
        {
            // La referencia DbContext selecciona el método EF, no el adaptador sin parámetros.
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new UpdateException(ex, ex.Entries.Select(e => e.Entity.GetType().Name));
        }
        catch (Exception)
        {
            throw;
        }
    }
}