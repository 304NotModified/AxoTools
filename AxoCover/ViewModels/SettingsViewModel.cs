﻿using AxoCover.Models;
using AxoCover.Models.Data;
using AxoCover.Models.Extensions;
using AxoCover.Properties;
using AxoCover.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace AxoCover.ViewModels
{
  public class SettingsViewModel : ViewModel
  {
    private readonly IEditorContext _editorContext;
    private readonly IOutputCleaner _outputCleaner;
    private readonly ITestRunner _testRunner;

    public PackageManifest Manifest
    {
      get
      {
        return AxoCoverPackage.Manifest;
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

    private bool _isAutoCoverEnabled = Settings.Default.IsAutoCoverEnabled;
    public bool IsAutoCoverEnabled
    {
      get
      {
        return _isAutoCoverEnabled;
      }
      set
      {
        _isAutoCoverEnabled = value;
        Settings.Default.IsAutoCoverEnabled = value;
        NotifyPropertyChanged(nameof(IsAutoCoverEnabled));
      }
    }

    private TestItemViewModel _testSolution;
    public TestItemViewModel TestSolution
    {
      get
      {
        return _testSolution;
      }
      set
      {
        _testSolution = value;
        NotifyPropertyChanged(nameof(TestSolution));
      }
    }

    private readonly ObservableEnumeration<string> _testSettingsFiles;
    public ObservableEnumeration<string> TestSettingsFiles
    {
      get
      {
        return _testSettingsFiles;
      }
    }

    private string _selectedTestSettings;
    public string SelectedTestSettings
    {
      get
      {
        return _selectedTestSettings;
      }
      set
      {
        _selectedTestSettings = value;
        NotifyPropertyChanged(nameof(SelectedTestSettings));
      }
    }

    public IEnumerable<string> TestRunners
    {
      get
      {
        return (_testRunner as IMultiplexer).Implementations;
      }
    }

    public string SelectedTestRunner
    {
      get
      {
        return (_testRunner as IMultiplexer).Implementation;
      }
      set
      {
        (_testRunner as IMultiplexer).Implementation = value;
        NotifyPropertyChanged(nameof(SelectedTestRunner));
      }
    }

    public ICommand OpenWebSiteCommand
    {
      get
      {
        return new DelegateCommand(p => Process.Start(Manifest.WebSite));
      }
    }

    public ICommand OpenLicenseDialogCommand
    {
      get
      {
        return new DelegateCommand(p =>
        {
          var dialog = new ViewDialog<TextView>()
          {
            Title = Manifest.Name + " " + Resources.License
          };
          dialog.View.ViewModel.Text = Manifest.License;
          dialog.ShowDialog();
        });
      }
    }

    public ICommand OpenReleaseNotesDialogCommand
    {
      get
      {
        return new DelegateCommand(p =>
        {
          var dialog = new ViewDialog<TextView>()
          {
            Title = Manifest.Name + " " + Resources.ReleaseNotes
          };
          dialog.View.ViewModel.Text = Manifest.ReleaseNotes;
          dialog.ShowDialog();
        });
      }
    }

    public ICommand CleanTestOutputCommand
    {
      get
      {
        return new DelegateCommand(async p =>
        {
          await _outputCleaner.CleanOutputAsync(p as TestOutputDescription);
          RefreshProjectSizes();
        });
      }
    }

    public ICommand OpenPathCommand
    {
      get
      {
        return new DelegateCommand(p => _editorContext.OpenPathInExplorer(p as string));
      }
    }

    public ICommand ClearTestSettingsCommand
    {
      get
      {
        return new DelegateCommand(
          p => SelectedTestSettings = null,
          p => SelectedTestSettings != null,
          p => ExecuteOnPropertyChange(p, nameof(SelectedTestSettings)));
      }
    }

    public ICommand NavigateToFileCommand
    {
      get
      {
        return new DelegateCommand(
          p =>
          {
            _editorContext.NavigateToFile(p as string);
          });
      }
    }

    public SettingsViewModel(IEditorContext editorContext, IOutputCleaner outputCleaner, ITestRunner testRunner)
    {
      _editorContext = editorContext;
      _outputCleaner = outputCleaner;
      _testRunner = testRunner;

      _testSettingsFiles = new ObservableEnumeration<string>(() =>
        _editorContext?.Solution.FindFiles(new Regex("^.*\\.testSettings$", RegexOptions.Compiled | RegexOptions.IgnoreCase)) ?? new string[0], StringComparer.OrdinalIgnoreCase.Compare);

      editorContext.BuildFinished += (o, e) => Refresh();
      editorContext.SolutionOpened += (o, e) => Refresh();
    }

    public async void RefreshProjectSizes()
    {
      if (TestSolution != null)
      {
        foreach (TestProjectViewModel testProject in TestSolution.Children.ToArray())
        {
          testProject.Output = await _outputCleaner.GetOutputFilesAsync(testProject.CodeItem as TestProject);
        }
      }
    }

    public void Refresh()
    {
      TestSettingsFiles.Refresh();
      RefreshProjectSizes();
    }
  }
}
