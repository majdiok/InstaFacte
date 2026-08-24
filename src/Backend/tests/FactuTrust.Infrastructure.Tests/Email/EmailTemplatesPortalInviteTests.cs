using FactuTrust.Infrastructure.Services.Email;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.EmailCatalog;

public sealed class EmailTemplatesPortalInviteTests
{
    [Fact]
    public void Portal_invite_template_is_catalogued()
    {
        var template = EmailTemplates.Get("portal-invite");
        Assert.NotNull(template);
        Assert.Contains("portal-invite", EmailTemplates.AllCodes);
        Assert.Contains("inviteUrl", template!.HtmlBodyTemplate);
    }
}
