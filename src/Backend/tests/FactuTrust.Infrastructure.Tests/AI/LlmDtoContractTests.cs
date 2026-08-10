using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Json;
using FactuTrust.Application.Features.Accounting.DocumentImport;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

/// <summary>
/// Garde-fou structurel contre le retour du bug d'origine.
///
/// Le défaut n'était pas un champ mal écrit, mais une RÈGLE non tenue : « les DTO de sortie LLM
/// sont tolérants ». Ces tests transforment la règle en contrainte vérifiée par réflexion, de sorte
/// qu'une propriété ajoutée demain avec un type non couvert fasse échouer la CI plutôt que la
/// production.
/// </summary>
public sealed class LlmDtoContractTests
{
    /// <summary>Les deux graphes de DTO de sortie LLM de la solution.</summary>
    private static readonly Type[] Roots = [typeof(LlmAccountingDocument), typeof(LlmInvoiceExtraction)];

    private static IEnumerable<PropertyInfo> WalkProperties(Type root)
    {
        var seen = new HashSet<Type>();
        var queue = new Queue<Type>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            if (!seen.Add(type))
                continue;

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                yield return property;

                foreach (var nested in NestedDtoTypes(property.PropertyType))
                {
                    if (nested.Namespace?.StartsWith("FactuTrust", StringComparison.Ordinal) == true)
                        queue.Enqueue(nested);
                }
            }
        }
    }

    private static IEnumerable<Type> NestedDtoTypes(Type type)
    {
        if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
        {
            foreach (var arg in type.GetGenericArguments())
                yield return arg;
            yield break;
        }
        yield return type;
    }

    [Fact]
    public void LlmDtoGraphs_HaveOnlyNullableValueTypeProperties()
    {
        var offenders = Roots
            .SelectMany(WalkProperties)
            .Where(p => p.PropertyType.IsValueType && Nullable.GetUnderlyingType(p.PropertyType) is null)
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name} ({p.PropertyType.Name})")
            .Distinct()
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Un type valeur non nullable dans un DTO de sortie LLM échoue dès que le modèle omet le "
            + "champ ou l'écrit dans un autre type. Rendez-le nullable : " + string.Join(", ", offenders));
    }

    [Fact]
    public void LlmJsonOptions_CoversEveryScalarTypeUsedByLlmDtos()
    {
        var lenient = new[]
        {
            typeof(LenientNullableInt32Converter),
            typeof(LenientNullableDecimalConverter),
            typeof(LenientStringConverter),
            typeof(LenientStringListConverter)
        };

        var scalarTypes = Roots
            .SelectMany(WalkProperties)
            .Select(p => p.PropertyType)
            .Where(t => t == typeof(string)
                        || t == typeof(List<string>)
                        || (t.IsValueType && Nullable.GetUnderlyingType(t) is not null))
            .Distinct()
            .ToList();

        Assert.NotEmpty(scalarTypes);

        var uncovered = scalarTypes
            .Where(t => LlmJsonOptions.Tolerant.GetConverter(t) is not JsonConverter c
                        || !lenient.Contains(c.GetType()))
            .Select(t => t.Name)
            .ToList();

        Assert.True(
            uncovered.Count == 0,
            "Ces types apparaissent dans un DTO LLM sans convertisseur tolérant — une valeur mal "
            + "typée y annulerait toute l'extraction : " + string.Join(", ", uncovered));
    }

    /// <summary>
    /// La tolérance doit rester confinée aux sorties de LLM : elle n'a rien à faire dans la
    /// sérialisation des API, où un type erroné doit continuer d'échouer bruyamment.
    /// </summary>
    [Fact]
    public void LlmJsonOptions_DoesNotLeakIntoTheDefaultSerializer()
    {
        Assert.IsNotType<LenientNullableInt32Converter>(
            JsonSerializerOptions.Default.GetConverter(typeof(int?)));
        Assert.IsNotType<LenientStringConverter>(
            JsonSerializerOptions.Default.GetConverter(typeof(string)));
    }

    [Fact]
    public void LlmJsonOptions_IsReadOnly_SoItCannotDriftAtRuntime()
    {
        Assert.True(LlmJsonOptions.Tolerant.IsReadOnly);
        Assert.Throws<InvalidOperationException>(
            () => LlmJsonOptions.Tolerant.Converters.Add(new LenientStringConverter()));
    }
}
