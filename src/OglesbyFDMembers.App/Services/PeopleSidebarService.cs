using Radzen;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OglesbyFDMembers.App.Services
{
    public class PeopleSidebarService
    {
        private readonly DialogService _dialogs;

        public PeopleSidebarService(DialogService dialogs)
        {
            _dialogs = dialogs;
        }

        public async Task<bool> OpenEditPersonDialog(int personId)
        {
            var result = await _dialogs.OpenSideAsync<OglesbyFDMembers.App.Views.People.Sidebars.EditPersonDialog>(
                "Edit Person",
                new Dictionary<string, object?> { ["PersonId"] = personId },
                DefaultOptions());
            return result is bool ok && ok;
        }

        public async Task<bool> OpenAddAliasDialog(int personId)
        {
            var result = await _dialogs.OpenSideAsync<OglesbyFDMembers.App.Views.People.Sidebars.AddAliasDialog>(
                "Add Alias",
                new Dictionary<string, object?> { ["PersonId"] = personId },
                DefaultOptions());
            return result is bool ok && ok;
        }

        public async Task<bool> OpenAddPropertyDialog(int personId)
        {
            var result = await _dialogs.OpenSideAsync<OglesbyFDMembers.App.Views.People.Sidebars.AddPropertyDialog>(
                "Add Property",
                new Dictionary<string, object?> { ["PersonId"] = personId },
                DefaultOptions());
            return result is bool ok && ok;
        }

        public async Task<bool> OpenAddAddressSidebar(int personId)
        {
            var result = await _dialogs.OpenSideAsync<OglesbyFDMembers.App.Views.People.Sidebars.AddMailingAddressSidebar>(
                "Add Mailing Address",
                new Dictionary<string, object?> { ["PersonId"] = personId },
                DefaultOptions());
            return result is bool ok && ok;
        }

        public async Task<bool> OpenAddPaymentDialog(int personId)
        {
            var result = await _dialogs.OpenSideAsync<OglesbyFDMembers.App.Views.Payments.AddPaymentDialog>(
                "Add Payment",
                new Dictionary<string, object?> { ["PersonId"] = personId },
                DefaultOptions());
            return result is bool ok && ok;
        }

        private static SideDialogOptions DefaultOptions() => new()
        {
            CloseDialogOnOverlayClick = false,
            ShowMask = true,
            Position = DialogPosition.Right
        };
    }
}

