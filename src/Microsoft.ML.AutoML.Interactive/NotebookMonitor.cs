﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Interactive;
using System.Collections.Generic;
using Microsoft.Data.Analysis;
using System;
using System.Threading.Tasks;
using System.Text.Json;
using System.Threading;

namespace Microsoft.ML.AutoML
{
    public class NotebookMonitor : IMonitor
    {
        private DisplayedValue _valueToUpdate;
        private DateTime _lastUpdate = DateTime.MinValue;

        public TrialResult BestTrial { get; set; }
        public TrialResult MostRecentTrial { get; set; }
        public TrialSettings ActiveTrial { get; set; }
        public List<TrialResult> CompletedTrials { get; set; }
        public DataFrame TrialData { get; set; }

        public NotebookMonitor()
        {
            CompletedTrials = new List<TrialResult>();
            TrialData = new DataFrame(new PrimitiveDataFrameColumn<int>("Trial"), new PrimitiveDataFrameColumn<float>("Metric"), new StringDataFrameColumn("Trainer"), new StringDataFrameColumn("Parameters"));
        }

        public void ReportBestTrial(TrialResult result)
        {
            BestTrial = result;
            Update();
        }

        public void ReportCompletedTrial(TrialResult result)
        {
            MostRecentTrial = result;
            CompletedTrials.Add(result);

            var activeRunParam = JsonSerializer.Serialize(result.TrialSettings.Parameter, new JsonSerializerOptions() { WriteIndented = false, });

            TrialData.Append(new List<KeyValuePair<string, object>>()
            {
                new KeyValuePair<string, object>("Trial",result.TrialSettings.TrialId),
                new KeyValuePair<string, object>("Metric", result.Metric),
                new KeyValuePair<string, object>("Trainer",result.TrialSettings.Pipeline.ToString().Replace("Unknown=>","")),
                new KeyValuePair<string, object>("Parameters",activeRunParam),
            }, true);
            Update();
        }

        public void ReportFailTrial(TrialResult result)
        {
            // TODO figure out what to do with failed trials.
            Update();
        }

        public void ReportRunningTrial(TrialSettings setting)
        {
            ActiveTrial = setting;
            Update();
        }

        private int _updatePending = 0;
        public void Update()
        {
            Task.Run(async () =>
            {
                if (Interlocked.CompareExchange(ref _updatePending, 1, 0) == 0) // _updatePending is int initialized with 0
                {
                    DateTime n = DateTime.UtcNow;
                    if (n - _lastUpdate < TimeSpan.FromSeconds(5))
                    {
                        await Task.Delay(n - _lastUpdate);
                    }

                    _valueToUpdate.Update(this);
                    _lastUpdate = n;
                    _updatePending = 0;
                }
            });

        }

        public void SetUpdate(DisplayedValue valueToUpdate)
        {
            _valueToUpdate = valueToUpdate;
            Update();
        }
    }
}
