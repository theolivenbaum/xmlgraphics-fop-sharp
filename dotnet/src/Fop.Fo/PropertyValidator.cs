// Licensed to the Apache Software Foundation (ASF) under one or more
// contributor license agreements.  See the NOTICE file distributed with
// this work for additional information regarding copyright ownership.
// The ASF licenses this file to You under the Apache License, Version 2.0
// (the "License"); you may not use this file except in compliance with
// the License.  You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Fop.Colors;

namespace Fop.Fo;

/// <summary>The seriousness of a property-validation finding.</summary>
public enum ValidationSeverity
{
    /// <summary>A recoverable issue (e.g. an unrecognised property name, which is ignored).</summary>
    Warning,

    /// <summary>An invalid value for a recognised property (e.g. a bad enum keyword or malformed length).</summary>
    Error,
}

/// <summary>
/// One finding from validating a property declaration: the offending property/value, the severity and
/// a human-readable message.
/// </summary>
/// <param name="PropertyName">The declared property name.</param>
/// <param name="Value">The declared (raw) value.</param>
/// <param name="Severity">The severity.</param>
/// <param name="Message">A human-readable description.</param>
public readonly record struct PropertyValidationMessage(
    string PropertyName, string Value, ValidationSeverity Severity, string Message);

/// <summary>
/// Validates the <em>declared</em> property values on a formatting object against the
/// <see cref="PropertyCatalog"/>: it flags unrecognised property names and values that do not match
/// their property's datatype (an out-of-set enum keyword, a malformed length/number/colour). This is
/// the role FOP's property "makers" play when they reject a value during refinement; here it is a
/// separate, non-fatal pass so a document still lays out (mirroring the lenient resolution in
/// <see cref="PropertyList"/>).
/// <para>
/// The universal keyword <c>inherit</c> and any value that looks like an XSL-FO expression are accepted
/// without datatype checking (they resolve dynamically). String/URI/stub/unknown datatypes are not
/// value-checked.
/// </para>
/// </summary>
public static class PropertyValidator
{
    // Length keywords accepted in place of an actual length on the common length properties.
    private static readonly HashSet<string> LengthKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "auto", "none", "normal", "baseline", "inherit",
        "thin", "medium", "thick",                                  // border widths
        "xx-small", "x-small", "small", "large", "x-large", "xx-large", "larger", "smaller", // font-size
        "smaller", "larger", "discard", "retain",
    };

    /// <summary>Validates every declared property on <paramref name="properties"/>.</summary>
    public static IReadOnlyList<PropertyValidationMessage> Validate(PropertyList properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        var messages = new List<PropertyValidationMessage>();
        foreach ((string name, string value) in properties.DeclaredProperties)
        {
            ValidateOne(name, value, messages);
        }

        return messages;
    }

    /// <summary>
    /// Validates every formatting object in the tree rooted at <paramref name="root"/> (depth-first,
    /// document order), concatenating all findings. A clean document returns an empty list.
    /// </summary>
    public static IReadOnlyList<PropertyValidationMessage> ValidateTree(FObj root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var messages = new List<PropertyValidationMessage>();
        Walk(root, messages);
        return messages;
    }

    private static void Walk(FObj obj, List<PropertyValidationMessage> messages)
    {
        foreach ((string name, string value) in obj.Properties.DeclaredProperties)
        {
            ValidateOne(name, value, messages);
        }

        foreach (FObj child in obj.ChildObjects)
        {
            Walk(child, messages);
        }
    }

    private static void ValidateOne(string name, string value, List<PropertyValidationMessage> messages)
    {
        // Namespaced attributes (xmlns:*, xml:*, fox:* handled elsewhere) and the universal "inherit"
        // keyword are not validated here.
        if (name.Contains(':'))
        {
            return;
        }

        PropertyDef? def = PropertyCatalog.Lookup(name);
        if (def is null)
        {
            messages.Add(new PropertyValidationMessage(name, value, ValidationSeverity.Warning,
                $"Unknown property '{name}'."));
            return;
        }

        string trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.Equals("inherit", StringComparison.OrdinalIgnoreCase)
            || PropertyList.LooksLikeExpression(trimmed))
        {
            return;
        }

        switch (def.Datatype)
        {
            case PropertyDatatype.Enum:
                if (def.EnumValues.Count > 0 && !def.EnumValues.Contains(trimmed.ToLowerInvariant()))
                {
                    messages.Add(new PropertyValidationMessage(name, value, ValidationSeverity.Error,
                        $"'{value}' is not a valid value for '{name}'. Expected one of: "
                        + string.Join(", ", def.EnumValues.OrderBy(v => v, StringComparer.Ordinal)) + "."));
                }

                break;

            case PropertyDatatype.Length:
            case PropertyDatatype.CondLength:
                ValidateLength(name, value, trimmed, messages);
                break;

            case PropertyDatatype.Number:
                if (!double.TryParse(trimmed, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    messages.Add(new PropertyValidationMessage(name, value, ValidationSeverity.Error,
                        $"'{value}' is not a valid number for '{name}'."));
                }

                break;

            case PropertyDatatype.Color:
                if (!LengthKeywords.Contains(trimmed) && TryParseColor(trimmed) is null)
                {
                    messages.Add(new PropertyValidationMessage(name, value, ValidationSeverity.Error,
                        $"'{value}' is not a valid colour for '{name}'."));
                }

                break;

            // String/Uri/Character/Space/LengthRange/Keep/List/Shorthand/FontFamily/Stub/Unknown are not
            // value-checked here (their grammars are accepted leniently and resolved at use).
            default:
                break;
        }
    }

    private static void ValidateLength(string name, string value, string trimmed,
        List<PropertyValidationMessage> messages)
    {
        // A conditional length carries an optional discard/retain keyword after the length.
        string head = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } parts
            ? parts[0]
            : trimmed;

        if (LengthKeywords.Contains(head) || head.EndsWith('%'))
        {
            return;
        }

        if (FoLength.TryParse(head, 12_000) is null)
        {
            messages.Add(new PropertyValidationMessage(name, value, ValidationSeverity.Error,
                $"'{value}' is not a valid length for '{name}'."));
        }
    }

    private static FopColor? TryParseColor(string value)
    {
        try
        {
            return ColorUtil.ParseColorString(null, value);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
