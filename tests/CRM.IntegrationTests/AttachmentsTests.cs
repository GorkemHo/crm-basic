using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CRM.IntegrationTests.Infrastructure;

namespace CRM.IntegrationTests;

public class AttachmentsTests(CrmApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Upload_List_Download_AndDelete()
    {
        var admin = await AsAdminAsync();
        var companyId = await CreateCompanyAsync(admin);

        var uploaded = await ReadJsonAsync(await UploadAsync(admin, "company", companyId, "notes.txt", "Hello, file!"));
        var id = uploaded.GetProperty("id").GetInt32();
        Assert.Equal("notes.txt", uploaded.GetProperty("fileName").GetString());
        Assert.Equal("text/plain", uploaded.GetProperty("contentType").GetString());
        Assert.Equal(12, uploaded.GetProperty("size").GetInt64());
        Assert.NotNull(uploaded.GetProperty("createdByName").GetString());

        var list = await admin.GetFromJsonAsync<JsonElement>($"/api/attachments?recordType=company&recordId={companyId}", Json);
        Assert.Equal(id, Assert.Single(list.EnumerateArray()).GetProperty("id").GetInt32());

        var download = await admin.GetAsync($"/api/attachments/{id}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("Hello, file!", await download.Content.ReadAsStringAsync());
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());

        var filesBefore = Directory.GetFiles(Factory.AttachmentsPath, "*", SearchOption.AllDirectories).Length;
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/attachments/{id}")).StatusCode);
        Assert.Equal(filesBefore - 1, Directory.GetFiles(Factory.AttachmentsPath, "*", SearchOption.AllDirectories).Length);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/attachments/{id}/download")).StatusCode);
    }

    [Fact]
    public async Task Upload_KeepsOnlyTheFileName_AndStoresUnderAGeneratedName()
    {
        var admin = await AsAdminAsync();
        var companyId = await CreateCompanyAsync(admin);

        var uploaded = await ReadJsonAsync(await UploadAsync(admin, "company", companyId, "../../etc/evil.pdf", "%PDF-1.4"));

        Assert.Equal("evil.pdf", uploaded.GetProperty("fileName").GetString());
        Assert.Equal("application/pdf", uploaded.GetProperty("contentType").GetString());
        Assert.DoesNotContain(Directory.GetFiles(Factory.AttachmentsPath, "*", SearchOption.AllDirectories), f => f.EndsWith("evil.pdf"));
    }

    [Fact]
    public async Task Upload_RefusesUnsafeTypes_EmptyAndTooLargeFiles()
    {
        var admin = await AsAdminAsync();
        var companyId = await CreateCompanyAsync(admin);

        await AssertProblemAsync(await UploadAsync(admin, "company", companyId, "page.html", "<script>alert(1)</script>"),
            HttpStatusCode.UnprocessableEntity, "attachment.type_not_allowed");
        await AssertProblemAsync(await UploadAsync(admin, "company", companyId, "image.svg", "<svg/>"),
            HttpStatusCode.UnprocessableEntity, "attachment.type_not_allowed");
        await AssertProblemAsync(await UploadAsync(admin, "company", companyId, "tool.exe", "MZ"),
            HttpStatusCode.UnprocessableEntity, "attachment.type_not_allowed");
        await AssertProblemAsync(await UploadAsync(admin, "company", companyId, "empty.txt", ""),
            HttpStatusCode.UnprocessableEntity, "attachment.empty");

        var tooLarge = new string('x', CrmApiFactory.MaxFileSizeMb * 1024 * 1024 + 1);
        await AssertProblemAsync(await UploadAsync(admin, "company", companyId, "big.txt", tooLarge),
            HttpStatusCode.UnprocessableEntity, "attachment.too_large");
    }

    [Fact]
    public async Task Upload_ToAMissingRecordOrUnknownType_Fails()
    {
        var admin = await AsAdminAsync();

        await AssertProblemAsync(await UploadAsync(admin, "deal", 999_999, "a.txt", "x"), HttpStatusCode.NotFound, "deal.not_found");
        await AssertProblemAsync(await UploadAsync(admin, "invoice", 1, "a.txt", "x"), HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task OnlyTheUploaderOrAManager_CanDelete()
    {
        var admin = await AsAdminAsync();
        var companyId = await CreateCompanyAsync(admin);
        var (uploaderEmail, uploaderPassword) = await RegisterUserAsync();
        var (otherEmail, otherPassword) = await RegisterUserAsync();
        var uploader = await AsUserAsync(uploaderEmail, uploaderPassword);
        var other = await AsUserAsync(otherEmail, otherPassword);

        var id = (await ReadJsonAsync(await UploadAsync(uploader, "company", companyId, "mine.txt", "mine"))).GetProperty("id").GetInt32();

        // Others can read it but not delete it.
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync($"/api/attachments/{id}/download")).StatusCode);
        await AssertProblemAsync(await other.DeleteAsync($"/api/attachments/{id}"), HttpStatusCode.Forbidden, "attachment.not_uploader");

        Assert.Equal(HttpStatusCode.NoContent, (await uploader.DeleteAsync($"/api/attachments/{id}")).StatusCode);

        var second = (await ReadJsonAsync(await UploadAsync(uploader, "company", companyId, "again.txt", "again"))).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/attachments/{second}")).StatusCode);
    }

    [Fact]
    public async Task RecordsWithFiles_CannotBeDeleted()
    {
        var admin = await AsAdminAsync();
        var companyId = await CreateCompanyAsync(admin);
        var id = (await ReadJsonAsync(await UploadAsync(admin, "company", companyId, "contract.pdf", "%PDF"))).GetProperty("id").GetInt32();

        await AssertProblemAsync(await admin.DeleteAsync($"/api/companies/{companyId}"), HttpStatusCode.Conflict, "company.has_attachments");

        await admin.DeleteAsync($"/api/attachments/{id}");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/companies/{companyId}")).StatusCode);
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, string recordType, int recordId, string fileName, string content)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        var form = new MultipartFormDataContent
        {
            { new StringContent(recordType), "recordType" },
            { new StringContent(recordId.ToString()), "recordId" },
            { file, "file", fileName }
        };

        return client.PostAsync("/api/attachments", form);
    }
}
