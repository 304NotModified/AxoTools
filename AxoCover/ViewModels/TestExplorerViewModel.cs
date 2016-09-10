﻿using AxoCover.Models;
using AxoCover.Models.Data;
using AxoCover.Models.Events;
using AxoCover.Models.Extensions;
using System;
using System.Linq;
using System.Windows.Input;

namespace AxoCover.ViewModels
{
  public class TestExplorerViewModel : ViewModel
  {
    private readonly IEditorContext _editorContext;
    private readonly ITestProvider _testProvider;
    private readonly ITestRunner _testRunner;
    private readonly IResultProvider _resultProvider;

    private bool _isSolutionLoaded;
    public bool IsSolutionLoaded
    {
      get
      {
        return _isSolutionLoaded;
      }
      set
      {
        _isSolutionLoaded = value;
        NotifyPropertyChanged(nameof(IsSolutionLoaded));
      }
    }

    public enum RunnerStates
    {
      Ready,
      Building,
      Testing
    }

    private RunnerStates _runnerState;
    public RunnerStates RunnerState
    {
      get
      {
        return _runnerState;
      }
      set
      {
        _runnerState = value;
        NotifyPropertyChanged(nameof(RunnerState));
        NotifyPropertyChanged(nameof(IsBusy));
        NotifyPropertyChanged(nameof(IsTesting));
      }
    }

    public bool IsBusy
    {
      get
      {
        return RunnerState == RunnerStates.Building || RunnerState == RunnerStates.Testing;
      }
    }

    public bool IsTesting
    {
      get
      {
        return RunnerState == RunnerStates.Testing;
      }
    }

    private bool _isProgressIndeterminate;
    public bool IsProgressIndeterminate
    {
      get
      {
        return _isProgressIndeterminate;
      }
      set
      {
        _isProgressIndeterminate = value;
        NotifyPropertyChanged(nameof(IsProgressIndeterminate));
      }
    }

    private int _testsToExecute;
    private int _testsExecuted;

    private double _Progress;
    public double Progress
    {
      get
      {
        return _Progress;
      }
      set
      {
        _Progress = value;
        NotifyPropertyChanged(nameof(Progress));
      }
    }

    private string _statusMessage = Resources.Ready;
    public string StatusMessage
    {
      get
      {
        return _statusMessage;
      }
      set
      {
        _statusMessage = value;
        NotifyPropertyChanged(nameof(StatusMessage));
      }
    }

    private bool _isAutoCoverEnabled;
    public bool IsAutoCoverEnabled
    {
      get
      {
        return _isAutoCoverEnabled;
      }
      set
      {
        _isAutoCoverEnabled = value;
        NotifyPropertyChanged(nameof(IsAutoCoverEnabled));
      }
    }

    public bool IsHighlighting
    {
      get
      {
        return LineCoverageAdornment.IsHighlighting;
      }
      set
      {
        LineCoverageAdornment.IsHighlighting = value;
        NotifyPropertyChanged(nameof(IsHighlighting));
      }
    }

    private TestItemViewModel _TestSolution;
    public TestItemViewModel TestSolution
    {
      get
      {
        return _TestSolution;
      }
      private set
      {
        _TestSolution = value;
        NotifyPropertyChanged(nameof(TestSolution));
      }
    }

    private TestItemViewModel _SelectedItem;
    public TestItemViewModel SelectedItem
    {
      get
      {
        return _SelectedItem;
      }
      set
      {
        _SelectedItem = value;
        NotifyPropertyChanged(nameof(SelectedItem));
        NotifyPropertyChanged(nameof(SelectedResult));
        NotifyPropertyChanged(nameof(IsItemSelected));
        NotifyPropertyChanged(nameof(IsTestSelected));
      }
    }

    public bool IsItemSelected
    {
      get
      {
        return SelectedItem != null;
      }
    }

    public bool IsTestSelected
    {
      get
      {
        return SelectedItem?.TestItem is TestMethod;
      }
    }

    public TestResult SelectedResult
    {
      get
      {
        if (SelectedItem?.TestItem.Kind == TestItemKind.Method)
        {
          return _resultProvider.GetTestResult(SelectedItem.TestItem as TestMethod);
        }
        else
        {
          return null;
        }
      }
    }

    public ICommand BuildCommand
    {
      get
      {
        return new DelegateCommand(
          p => _editorContext.BuildSolution(),
          p => !IsBusy,
          p => ExecuteOnPropertyChange(p, nameof(IsBusy)));
      }
    }

    public ICommand ExpandAllCommand
    {
      get
      {
        return new DelegateCommand(p => TestSolution.ExpandAll());
      }
    }

    public ICommand CollapseAllCommand
    {
      get
      {
        return new DelegateCommand(p => TestSolution.CollapseAll());
      }
    }

    public ICommand RunTestsCommand
    {
      get
      {
        return new DelegateCommand(
          p =>
          {
            _testRunner.RunTestsAsync(SelectedItem.TestItem);
            SelectedItem.ScheduleAll();
          },
          p => !IsBusy && SelectedItem != null,
          p => ExecuteOnPropertyChange(p, nameof(IsBusy), nameof(SelectedItem)));
      }
    }

    public ICommand NavigateToTestItemCommand
    {
      get
      {
        return new DelegateCommand(
          p =>
          {
            var testItem = p as TestItem;
            switch (testItem.Kind)
            {
              case TestItemKind.Class:
                _editorContext.NavigateToClass(testItem.GetParent<TestProject>().Name, testItem.FullName);
                break;
              case TestItemKind.Method:
                _editorContext.NavigateToMethod(testItem.GetParent<TestProject>().Name, testItem.Parent.FullName, testItem.Name);
                break;
            }
          },
          p => p.CheckAs<TestItem>(q => q.Kind == TestItemKind.Class || q.Kind == TestItemKind.Method));
      }
    }

    public ICommand NavigateToStackItemCommand
    {
      get
      {
        return new DelegateCommand(
          p =>
          {
            var stackItem = p as StackItem;
            if (stackItem.HasFileReference)
            {
              _editorContext.NavigateToFile(stackItem.SourceFile, stackItem.Line);
            }
          });
      }
    }

    public TestExplorerViewModel(IEditorContext editorContext, ITestProvider testProvider, ITestRunner testRunner, IResultProvider resultProvider)
    {
      _editorContext = editorContext;
      _testProvider = testProvider;
      _testRunner = testRunner;
      _resultProvider = resultProvider;

      _editorContext.SolutionOpened += OnSolutionOpened;
      _editorContext.SolutionClosing += OnSolutionClosing;
      _editorContext.BuildStarted += OnBuildStarted;
      _editorContext.BuildFinished += OnBuildFinished;

      _testRunner.TestsStarted += OnTestsStarted;
      _testRunner.TestExecuted += OnTestExecuted;
      _testRunner.TestLogAdded += OnTestLogAdded;
      _testRunner.TestsFinished += OnTestsFinished;

      _resultProvider.ResultsUpdated += OnResultsUpdated;
    }

    private async void OnSolutionOpened(object sender, EventArgs e)
    {
      var testSolution = await _testProvider.GetTestSolutionAsync(_editorContext.Solution);
      Update(testSolution);
      IsSolutionLoaded = true;
    }

    private void OnSolutionClosing(object sender, EventArgs e)
    {
      IsSolutionLoaded = false;
      Update(null);
    }

    private void OnBuildStarted(object sender, EventArgs e)
    {
      IsProgressIndeterminate = true;
      StatusMessage = Resources.Building;
      RunnerState = RunnerStates.Building;
    }

    private async void OnBuildFinished(object sender, EventArgs e)
    {
      IsProgressIndeterminate = false;
      StatusMessage = Resources.Done;
      RunnerState = RunnerStates.Ready;
      IsSolutionLoaded = true;
      var testSolution = await _testProvider.GetTestSolutionAsync(_editorContext.Solution);
      Update(testSolution);

      if (IsAutoCoverEnabled && RunTestsCommand.CanExecute(null))
      {
        RunTestsCommand.Execute(null);
      }
    }

    private void OnTestsStarted(object sender, EventArgs e)
    {
      _testsToExecute = SelectedItem.TestItem.TestCount;
      _testsExecuted = 0;
      IsProgressIndeterminate = true;
      StatusMessage = Resources.InitializingTestRunner;
      RunnerState = RunnerStates.Testing;
      TestSolution.ResetAll();
      _editorContext.ClearLog();
      _editorContext.ActivateLog();
    }

    private void OnTestExecuted(object sender, TestExecutedEventArgs e)
    {
      var itemPath = e.Path.Split('.');

      var itemName = string.Empty;
      var testItem = TestSolution;
      foreach (var part in itemPath)
      {
        if (itemName != string.Empty)
        {
          itemName += ".";
        }
        itemName += part;

        var childItem = testItem.Children.FirstOrDefault(p => p.TestItem.Name == itemName);

        if (childItem != null)
        {
          itemName = string.Empty;
          testItem = childItem;
        }
      }

      if (testItem != null && itemName == string.Empty)
      {
        testItem.State = e.Outcome;
        _testsExecuted++;
      }

      if (_testsExecuted < _testsToExecute)
      {
        IsProgressIndeterminate = false;
        Progress = (double)_testsExecuted / _testsToExecute;
        StatusMessage = string.Format(Resources.ExecutingTests, _testsExecuted, _testsToExecute);
      }
      else
      {
        IsProgressIndeterminate = true;
        StatusMessage = Resources.GeneratingCoverageReport;
      }
    }

    private void OnTestLogAdded(object sender, TestLogAddedEventArgs e)
    {
      _editorContext.WriteToLog(e.Text);
    }

    private void OnTestsFinished(object sender, TestFinishedEventArgs e)
    {
      IsProgressIndeterminate = false;
      StatusMessage = Resources.Done;
      RunnerState = RunnerStates.Ready;
    }

    private void OnResultsUpdated(object sender, EventArgs e)
    {
      NotifyPropertyChanged(nameof(SelectedResult));
    }

    private void Update(TestSolution testSolution)
    {
      if (testSolution != null)
      {
        if (TestSolution == null)
        {
          TestSolution = new TestItemViewModel(null, testSolution);
        }
        else
        {
          TestSolution.UpdateItem(testSolution);
        }
      }
      else
      {
        TestSolution = null;
      }
    }
  }
}