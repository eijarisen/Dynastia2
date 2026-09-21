using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Dynastia.App.ViewModels;
using Dynastia.Contracts;

namespace Dynastia.App.Views;

public partial class TownLifeWindow : Window
{
    public TownLifeWindow()
    {
        InitializeComponent();
    }

    public TownLifeWindow(TownAffairsViewModel model)
        : this()
    {
        DataContext = model;
        Title = model.Snapshot.NavigationLabel;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnInstitutionPointerPressed(
        object? sender,
        PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            || DataContext is not TownAffairsViewModel model
            || sender is not Border { DataContext: TownInstitutionAffairsInfo institution })
        {
            return;
        }

        if (model.OpenInstitution(institution.InstitutionId))
            e.Handled = true;
    }

    private void OnMemberTabClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TownAffairsViewModel model
            || sender is not Button { DataContext: TownAffairsMemberTabViewModel member })
        {
            return;
        }

        model.SelectSubject(member.PersonId);
    }

    private void OnBuyHouseOfferClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TownAffairsViewModel model
            || sender is not Button { DataContext: TownAffairsHouseOfferViewModel offer }
            || !offer.IsAffordable)
        {
            return;
        }

        model.QueueHousePurchase(offer);
        CloseIfQueued(model);
    }

    private void OnApplyJobClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TownAffairsViewModel model
            || sender is not Button { DataContext: JobOpportunityCardViewModel card })
        {
            return;
        }

        model.QueueJob(card.Opportunity);
        CloseIfQueued(model);
    }

    private async void OnTakeLoanClick(
        object? sender,
        RoutedEventArgs e) =>
        await OpenLoanWindow(isGivingLoan: false);

    private async void OnGiveLoanClick(
        object? sender,
        RoutedEventArgs e) =>
        await OpenLoanWindow(isGivingLoan: true);

    private async Task OpenLoanWindow(bool isGivingLoan)
    {
        if (DataContext is not TownAffairsViewModel model
            || (isGivingLoan && !model.CanGiveLoan)
            || (!isGivingLoan && !model.CanTakeLoan))
        {
            return;
        }

        var offers = model.GetLoanOffers(isGivingLoan);
        if (offers.Count == 0)
            return;

        var window = new LoanSelectionWindow(
            isGivingLoan,
            offers);
        var selection = await window.ShowDialog<LoanSelectionResult?>(this);
        if (selection is null)
            return;

        model.QueueLoan(isGivingLoan, selection);
        CloseIfQueued(model);
    }

    private void OnEducationClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TownAffairsViewModel model
            || sender is not Button { DataContext: PropertySelectionOption option }
            || !option.IsEnabled)
        {
            return;
        }

        model.QueueEducation(option.Id);
        CloseIfQueued(model);
    }

    private void OnHealthActionClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TownAffairsViewModel model)
            return;

        if (sender is not Button { DataContext: TownAffairsHealthActionViewModel action }
            || !action.IsAvailable)
        {
            return;
        }

        model.QueueHealth(action.ActionId);
        CloseIfQueued(model);
    }

    private void CloseIfQueued(TownAffairsViewModel model)
    {
        if (model.HasQueuedAction)
            Close();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        e.Handled = true;
        Close();
    }
}
