using Microsoft.UI.Reactor.Cli.Loc;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Microsoft.UI.Reactor.Tests.Localization;

public class InterpolationConverterTests
{
    private static InterpolatedStringExpressionSyntax ParseInterpolation(string interpolatedString)
    {
        var source = $"class C {{ void M() {{ var x = {interpolatedString}; }} }}";
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();
        return root.DescendantNodes()
            .OfType<InterpolatedStringExpressionSyntax>()
            .First();
    }

    [Fact]
    public void SimpleVariable_PreservedAsIs()
    {
        var node = ParseInterpolation("$\"Hello, {name}\"");
        var (icu, argMap, warnings) = InterpolationConverter.Convert(node);

        Assert.Equal("Hello, {name}", icu);
        Assert.Null(argMap); // name maps to itself
        Assert.Empty(warnings);
    }

    [Fact]
    public void DottedExpression_LastSegmentCamelCased()
    {
        var node = ParseInterpolation("$\"Hello, {user.Name}\"");
        var (icu, argMap, warnings) = InterpolationConverter.Convert(node);

        Assert.Equal("Hello, {name}", icu);
        Assert.NotNull(argMap);
        Assert.Equal("user.Name", argMap!["name"]);
        Assert.Empty(warnings);
    }

    [Fact]
    public void CurrencyFormat_ConvertedToIcuNumber()
    {
        var node = ParseInterpolation("$\"Total: {price:C}\"");
        var (icu, _, warnings) = InterpolationConverter.Convert(node);

        Assert.Equal("Total: {price, number, currency}", icu);
        Assert.Empty(warnings);
    }

    [Fact]
    public void PercentFormat_ConvertedToIcuNumber()
    {
        var node = ParseInterpolation("$\"Score: {pct:P0}\"");
        var (icu, _, warnings) = InterpolationConverter.Convert(node);

        Assert.Equal("Score: {pct, number, percent}", icu);
    }

    [Fact]
    public void FixedPointFormat_ConvertedToIcuNumber()
    {
        var node = ParseInterpolation("$\"Size: {fontSize:F0}px\"");
        var (icu, _, warnings) = InterpolationConverter.Convert(node);

        Assert.Equal("Size: {fontSize, number}px", icu);
        Assert.Empty(warnings);
    }

    [Fact]
    public void FixedPointFormatUpperF_ConvertedToIcuNumber()
    {
        var node = ParseInterpolation("$\"Value: {x:F2}\"");
        var (icu, _, warnings) = InterpolationConverter.Convert(node);

        Assert.Equal("Value: {x, number}", icu);
        Assert.Empty(warnings);
    }

    [Fact]
    public void DateShortFormat_ConvertedToIcuDate()
    {
        var node = ParseInterpolation("$\"Due: {date:d}\"");
        var (icu, _, warnings) = InterpolationConverter.Convert(node);

        Assert.Equal("Due: {date, date, short}", icu);
    }

    [Fact]
    public void DateLongFormat_ConvertedToIcuDate()
    {
        var node = ParseInterpolation("$\"Date: {date:D}\"");
        var (icu, _, warnings) = InterpolationConverter.Convert(node);

        Assert.Equal("Date: {date, date, long}", icu);
    }

    [Fact]
    public void MethodCall_WarnsComplexExpression()
    {
        var node = ParseInterpolation("$\"Total: {GetTotal()}\"");
        var (icu, _, warnings) = InterpolationConverter.Convert(node);

        Assert.NotNull(icu);
        Assert.NotEmpty(warnings);
        Assert.Contains("Complex expression", warnings[0]);
    }

    [Fact]
    public void MultipleVariables_AllConverted()
    {
        var node = ParseInterpolation("$\"{count} items by {author}\"");
        var (icu, _, warnings) = InterpolationConverter.Convert(node);

        Assert.Equal("{count} items by {author}", icu);
        Assert.Empty(warnings);
    }

    [Fact]
    public void ParenthesizedTernary_WithStringLiterals_ConvertsToIcuSelect()
    {
        var node = ParseInterpolation("$\"Dark mode: {(darkMode ? \"Yes\" : \"No\")}\"");
        var (icu, argMap, warnings) = InterpolationConverter.Convert(node);

        Assert.Equal("Dark mode: {darkMode, select, true {Yes} false {No}}", icu);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Ternary_WithMemberAccessCondition_ConvertsToIcuSelect()
    {
        var node = ParseInterpolation("$\"Mode: {(settings.DarkMode ? \"Yes\" : \"No\")}\"");
        var (icu, argMap, warnings) = InterpolationConverter.Convert(node);

        Assert.Equal("Mode: {darkMode, select, true {Yes} false {No}}", icu);
        Assert.NotNull(argMap);
        Assert.Equal("settings.DarkMode", argMap!["darkMode"]);
        Assert.Empty(warnings);
    }

    [Fact]
    public void QuantitySuffixTernary_ConvertsToIcuPlural()
    {
        var node = ParseInterpolation("$\"{remaining} item{(remaining == 1 ? \"\" : \"s\")} left\"");
        var (icu, argMap, warnings) = InterpolationConverter.Convert(node);

        Assert.Equal("{remaining, plural, one {# item} other {# items}} left", icu);
        Assert.Null(argMap);
        Assert.Empty(warnings);
    }

    [Fact]
    public void QuantitySuffixTernary_WithNotEquals_ConvertsToIcuPlural()
    {
        var node = ParseInterpolation("$\"{remaining} item{(remaining != 1 ? \"s\" : \"\")} left\"");
        var (icu, _, warnings) = InterpolationConverter.Convert(node);

        Assert.Equal("{remaining, plural, one {# item} other {# items}} left", icu);
        Assert.Empty(warnings);
    }

    [Fact]
    public void QuantityTernary_WithFullWords_ConvertsToIcuPlural()
    {
        var node = ParseInterpolation("$\"{remaining == 1 ? \"item\" : \"items\"}\"");
        var (icu, _, warnings) = InterpolationConverter.Convert(node);

        Assert.Equal("{remaining, plural, one {# item} other {# items}}", icu);
        Assert.Empty(warnings);
    }

    [Fact]
    public void QuantitySuffixTernary_WithoutAdjacentQuantity_StillUsesIcuPlural()
    {
        var node = ParseInterpolation("$\"item{remaining == 1 ? \"\" : \"s\"}\"");
        var (icu, _, warnings) = InterpolationConverter.Convert(node);

        Assert.Equal("item{remaining, plural, one {} other {s}}", icu);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Ternary_WithNonLiteralBranch_StillWarnsComplex()
    {
        var node = ParseInterpolation("$\"Value: {(flag ? GetValue() : \"default\")}\"");
        var (icu, _, warnings) = InterpolationConverter.Convert(node);

        Assert.NotNull(icu);
        Assert.NotEmpty(warnings);
        Assert.Contains("Complex expression", warnings[0]);
    }

    [Theory]
    [InlineData("count", true)]
    [InlineData("total", true)]
    [InlineData("numItems", true)]
    [InlineData("totalCount", true)]
    [InlineData("name", false)]
    [InlineData("description", false)]
    public void IsQuantityName_CorrectResults(string name, bool expected)
    {
        Assert.Equal(expected, InterpolationConverter.IsQuantityName(name));
    }
}
