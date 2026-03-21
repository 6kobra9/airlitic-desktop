using System;
using System.Windows;

namespace AirLiticApp;

public partial class ReportPeriodWindow : Window
{
    public DateTime PeriodFrom { get; private set; }
    public DateTime PeriodTo { get; private set; }

    public ReportPeriodWindow()
    {
        InitializeComponent();
        var today = DateTime.Today;
        FromDatePicker.SelectedDate = today;
        ToDatePicker.SelectedDate = today;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        var from = FromDatePicker.SelectedDate?.Date ?? DateTime.Today;
        var to = ToDatePicker.SelectedDate?.Date ?? DateTime.Today;
        if (from > to)
            (from, to) = (to, from);

        PeriodFrom = from;
        PeriodTo = to;
        DialogResult = true;
    }
}
