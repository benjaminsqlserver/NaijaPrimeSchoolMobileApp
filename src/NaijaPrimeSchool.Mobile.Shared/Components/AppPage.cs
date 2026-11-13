using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using NaijaPrimeSchool.Mobile.Shared.Services;
using NaijaPrimeSchool.Mobile.Shared.Services.Api;
using Radzen;

namespace NaijaPrimeSchool.Mobile.Shared.Components;

// Base class for every page: loading / error state, toasts, confirmations
// and the signed-in user, so pages stay focused on their own screen.
public abstract class AppPage : ComponentBase
{
    [Inject] protected NotificationService Notifications { get; set; } = default!;
    [Inject] protected DialogService Dialogs { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected AppState App { get; set; } = default!;

    [CascadingParameter] protected Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    protected bool Loading { get; set; } = true;
    protected bool Busy { get; set; }
    protected string? LoadError { get; set; }
    protected ClaimsPrincipal CurrentUser { get; private set; } = new(new ClaimsIdentity());

    protected override async Task OnInitializedAsync()
    {
        if (AuthenticationStateTask is not null)
            CurrentUser = (await AuthenticationStateTask).User;
        await App.EnsureConfigAsync();
    }

    protected bool IsInAnyRole(string roleGroup) =>
        NaijaPrimeSchool.Mobile.Contracts.Common.RoleGroups.Split(roleGroup).Any(CurrentUser.IsInRole);

    protected Guid? CurrentUserId =>
        Guid.TryParse(CurrentUser.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // Runs a load step, capturing API failures as LoadError for <PageState>.
    protected async Task LoadAsync(Func<Task> work)
    {
        Loading = true;
        LoadError = null;
        try
        {
            await work();
        }
        catch (ApiException ex)
        {
            LoadError = ex.Message;
        }
        finally
        {
            Loading = false;
            StateHasChanged();
        }
    }

    // Runs a command with a busy flag; shows the API's messages on failure.
    protected async Task<bool> RunAsync(Func<Task<ApiResult>> command, string? successMessage = null)
    {
        Busy = true;
        try
        {
            var result = await command();
            return Report(result.Succeeded, result.Errors, successMessage);
        }
        finally
        {
            Busy = false;
            StateHasChanged();
        }
    }

    protected async Task<ApiResult<T>> RunAsync<T>(Func<Task<ApiResult<T>>> command, string? successMessage = null)
    {
        Busy = true;
        try
        {
            var result = await command();
            Report(result.Succeeded, result.Errors, successMessage);
            return result;
        }
        finally
        {
            Busy = false;
            StateHasChanged();
        }
    }

    protected bool Report(bool succeeded, IReadOnlyList<string> errors, string? successMessage)
    {
        if (succeeded)
        {
            if (successMessage is not null) Success(successMessage);
        }
        else
        {
            Error(errors.Count == 0 ? "Something went wrong." : string.Join(" ", errors));
        }
        return succeeded;
    }

    protected void Success(string message) => Notifications.Notify(new NotificationMessage
    {
        Severity = NotificationSeverity.Success, Summary = message, Duration = 3000,
    });

    protected void Error(string message, string summary = "Could not complete") => Notifications.Notify(new NotificationMessage
    {
        Severity = NotificationSeverity.Error, Summary = summary, Detail = message, Duration = 6000,
    });

    protected async Task<bool> ConfirmAsync(string message, string title = "Please confirm", string ok = "Yes")
    {
        var answer = await Dialogs.Confirm(message, title, new ConfirmOptions
        {
            OkButtonText = ok,
            CancelButtonText = "Cancel",
            Width = "min(420px, 92vw)",
        });
        return answer == true;
    }
}
