using System.ComponentModel;
using System.Reflection;

namespace NaijaPrimeSchool.Mobile.Api.Endpoints;

// Binds an Application-layer filter class (StudentListFilter, InboxFilter,
// ...) from the query string, keeping the class's own defaults (Take = 20,
// Open = true, IncludeExpired = true, ...) for anything the caller leaves
// out. [AsParameters] would instead treat every non-nullable property as a
// required query parameter.
public sealed class QueryFilter<T> where T : new()
{
    private static readonly PropertyInfo[] Properties = typeof(T)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanWrite)
        .ToArray();

    public T Value { get; private init; } = new();

    public static ValueTask<QueryFilter<T>?> BindAsync(HttpContext context)
    {
        var value = new T();
        foreach (var property in Properties)
        {
            if (!context.Request.Query.TryGetValue(property.Name, out var raw) || string.IsNullOrWhiteSpace(raw)) continue;

            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            try
            {
                var converted = TypeDescriptor.GetConverter(type).ConvertFromInvariantString(raw.ToString());
                property.SetValue(value, converted);
            }
            catch (Exception ex) when (ex is FormatException or NotSupportedException or ArgumentException)
            {
                throw new BadHttpRequestException($"Invalid value for '{property.Name}'.");
            }
        }
        return ValueTask.FromResult<QueryFilter<T>?>(new QueryFilter<T> { Value = value });
    }
}
