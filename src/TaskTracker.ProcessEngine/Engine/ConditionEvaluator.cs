using System.Globalization;
using TaskTracker.ProcessEngine.Models;

namespace TaskTracker.ProcessEngine.Engine;

/// <summary>
/// Вычисление предикатов и условий переходов в ДНФ
/// </summary>
public class ConditionEvaluator
{
    private readonly ClassRegistry _classes;

    public ConditionEvaluator(ClassRegistry classes) => _classes = classes;

    /// <summary>
    /// Вычислить значение одного предиката для объекта заданного класса
    /// </summary>
    public bool EvaluatePredicate(PredicateDefinition pred, string classId, Dictionary<string, object?> values)
    {
        if (pred.Class != null) return _classes.IsOfClass(classId, pred.Class);

        object? v = null;
        if (pred.Parameter != null) values.TryGetValue(pred.Parameter, out v);

        if (pred.Operator == "filled") return v != null;
        if (v == null) return false;

        if (pred.Operator == "in_range")
        {
            var def = _classes.GetEffectiveParameters(classId).FirstOrDefault(p => p.Id == pred.Parameter);
            if (def == null || !TryDecimal(v, out var d)) return false;
            if (def.Min.HasValue && d < def.Min.Value) return false;
            if (def.Max.HasValue && d > def.Max.Value) return false;
            return true;
        }

        if (v is string s && pred.Operator is "=" or "!=")
        {
            var rhs = Convert.ToString(pred.Value, CultureInfo.InvariantCulture);
            return pred.Operator == "=" ? s == rhs : s != rhs;
        }

        if (!TryDecimal(v, out var l) || !TryDecimal(pred.Value, out var r)) return false;
        return pred.Operator switch
        {
            "=" => l == r,
            "!=" => l != r,
            "<" => l < r,
            "<=" => l <= r,
            ">" => l > r,
            ">=" => l >= r,
            _ => false
        };
    }

    /// <summary>
    /// Вычислить условие в ДНФ (внешний список = ИЛИ, внутренний = И, "not" отрицает предикат)
    /// </summary>
    public bool EvaluateCondition(List<List<string>>? condition,
        Dictionary<string, PredicateDefinition> predicateMap, string classId, Dictionary<string, object?> values)
    {
        if (condition == null || condition.Count == 0) return true;
        foreach (var conj in condition)
        {
            bool all = true;
            foreach (var raw in conj)
            {
                var term = raw.Trim();
                bool negate = term.StartsWith("not ");
                var id = negate ? term[4..].Trim() : term;
                if (!predicateMap.TryGetValue(id, out var pred))
                    throw new InvalidOperationException($"Предикат '{id}' не определён в шаблоне");
                if (EvaluatePredicate(pred, classId, values) == negate) { all = false; break; }
            }
            if (all) return true;
        }
        return false;
    }

    internal static bool TryDecimal(object? v, out decimal d)
    {
        switch (v)
        {
            case int or long or short or byte or float or double or decimal:
                d = Convert.ToDecimal(v, CultureInfo.InvariantCulture); return true;
            case string s:
                return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out d);
            default: d = 0; return false;
        }
    }
}
