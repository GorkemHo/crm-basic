using CRM.BuildingBlocks.Exceptions;
using CRM.Modules.Attachments.Features;

namespace CRM.UnitTests.Attachments;

public class FileNameTests
{
    [Theory]
    [InlineData("report.pdf", "report.pdf")]
    [InlineData("../../etc/passwd.txt", "passwd.txt")]
    [InlineData(@"C:\Users\me\Desktop\teklif.docx", "teklif.docx")]
    [InlineData("  spaced name.xlsx  ", "spaced name.xlsx")]
    [InlineData("tab\tand\u0000null.txt", "tabandnull.txt")]
    [InlineData("Şantiye raporu.pdf", "Şantiye raporu.pdf")]
    public void CleanFileName_KeepsOnlyASafeLastSegment(string input, string expected)
    {
        Assert.Equal(expected, AttachmentRules.CleanFileName(input));
    }

    [Fact]
    public void CleanFileName_ShortensLongNames_KeepingTheExtension()
    {
        var cleaned = AttachmentRules.CleanFileName(new string('a', 300) + ".pdf");

        Assert.Equal(255, cleaned.Length);
        Assert.EndsWith(".pdf", cleaned);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../")]
    [InlineData("..")]
    public void CleanFileName_WithoutAName_IsRefused(string input)
    {
        var error = Assert.Throws<BusinessRuleException>(() => AttachmentRules.CleanFileName(input));
        Assert.Equal("attachment.no_name", error.ErrorCode);
    }
}
