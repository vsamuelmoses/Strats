using System;
using System.Collections.Generic;
using System.ComponentModel;
using Carvers.Charting.Annotations;
using SciChart.Charting;
using SciChart.Charting.Model.ChartSeries;
using SciChart.Charting.Visuals.Annotations;

namespace Carvers.Charting.ViewModels
{
    internal class ArrowAnnotationViewModel : BaseAnnotationViewModel
    {
        public override Type ViewType => typeof(DownArrowAnnotation);
    }
}