using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Architecture;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CreateBeamAxis.Infrastructure;
using CreateBeamAxis.Models;

namespace CreateBeamAxis.ViewModels
{
    internal class MainWindowViewModel : Base.ViewModel
    {
        private RevitModelForfard _revitModel;

        internal RevitModelForfard RevitModel
        {
            get => _revitModel;
            set => _revitModel = value;
        }

        #region Заголовок
        private string _title = "Оси блоков пролетного строения";

        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }
        #endregion

        #region Элементы оси трассы
        private string _roadAxisElemIds;

        public string RoadAxisElemIds
        {
            get => _roadAxisElemIds;
            set => Set(ref _roadAxisElemIds, value);
        }
        #endregion

        #region Линии границ блоков
        private string _sectionLinesElemIds;

        public string SectionLinesElemIds
        {
            get => _sectionLinesElemIds;
            set => Set(ref _sectionLinesElemIds, value);
        }
        #endregion

        #region Название стилей линий
        private ObservableCollection<string> _graphicStyleNames;

        public ObservableCollection<string> GraphicStyleNames
        {
            get => _graphicStyleNames;
            set => Set(ref _graphicStyleNames, value);
        }
        #endregion

        #region Список настроек осей балок
        private ObservableCollection<BeamAxis> _beamAxisSetups;

        public ObservableCollection<BeamAxis> BeamAxisSetups
        {
            get => _beamAxisSetups;
            set => Set(ref _beamAxisSetups, value);
        }
        #endregion

        #region Команды

        #region Получение оси трассы
        public ICommand GetRoadAxisCommand { get; }

        private void OnGetRoadAxisCommandExecuted(object parameter)
        {
            RevitCommand.mainView.Hide();
            RevitModel.GetPolyCurve();
            RoadAxisElemIds = RevitModel.RoadAxisElemIds;
            RevitCommand.mainView.ShowDialog();
        }

        private bool CanGetRoadAxisCommandExecute(object parameter)
        {
            return true;
        }
        #endregion

        #region Получение линий границ блоков
        public ICommand GetSectionLinesCommand { get; }

        private void OnGetSectionLinesCommandExecuted(object parameter)
        {
            RevitCommand.mainView.Hide();
            RevitModel.GetSectionLines();
            SectionLinesElemIds = RevitModel.SectionLinesElemIds;
            RevitCommand.mainView.ShowDialog();
        }

        private bool CanGetSectionLinesCommandExecute(object parameter)
        {
            return true;
        }
        #endregion

        #region Добавление настройки оси
        public ICommand AddBeamAxisCommand { get; }

        private void OnAddBeamAxisCommandExecuted(object parameter)
        {
            var beamAxis = new BeamAxis
            {
                Distance = 2.5
            };

            BeamAxisSetups.Add(beamAxis);
        }

        private bool CanAddBeamAxisCommandExecuted(object parameter)
        {
            return true;
        }
        #endregion

        #region Удаление настройки оси из таблицы
        public ICommand DeleteBeamAxisCommand { get; }

        private void OnDeleteBeamAxisCommandExecuted(object parameter)
        {
            var lastBeamAxisSetup = BeamAxisSetups.LastOrDefault();

            if(!(lastBeamAxisSetup is null))
            {
                BeamAxisSetups.Remove(lastBeamAxisSetup);
            }
        }

        private bool CanDeleteBeamAxisCommandExecuted(object parameter)
        {
            if(BeamAxisSetups is null || BeamAxisSetups.Count == 0)
            {
                return false;
            }

            return true;
        }
        #endregion

        #region Создание осей блоков ПС

        #endregion

        #region Закрыть окно
        public ICommand CloseWindowCommand { get; }

        private void OnCloseWindowCommandExecuted(object parameter)
        {
            SaveSettings();
            RevitCommand.mainView.Close();
        }

        private bool CanCloseWindowCommandExecute(object parameter)
        {
            return true;
        }
        #endregion

        #endregion

        private void SaveSettings()
        {
            Properties.Settings.Default["RoadAxisElemIds"] = RoadAxisElemIds;
            Properties.Settings.Default["SectionLinesElemIds"] = SectionLinesElemIds;
            Properties.Settings.Default.Save();
        }


        #region Конструктор класса MainWindowViewModel
        public MainWindowViewModel(RevitModelForfard revitModel)
        {
            RevitModel = revitModel;

            BeamAxisSetups = new ObservableCollection<BeamAxis>
            {
                new BeamAxis() { Distance = 5.5 }
            };

            #region Инициализация значения оси из Settings
            if (!(Properties.Settings.Default["RoadAxisElemIds"] is null))
            {
                string axisElemIdInSettings = Properties.Settings.Default["RoadAxisElemIds"].ToString();
                if(RevitModel.IsLinesExistInModel(axisElemIdInSettings) && !string.IsNullOrEmpty(axisElemIdInSettings))
                {
                    RoadAxisElemIds = axisElemIdInSettings;
                    RevitModel.GetAxisBySettings(axisElemIdInSettings);
                }
            }
            #endregion

            #region Инициализация значения линиям границ блоков
            if (!(Properties.Settings.Default["SectionLinesElemIds"] is null))
            {
                string sectionLinesElemIdInSettings = Properties.Settings.Default["SectionLinesElemIds"].ToString();
                if(RevitModel.IsProfileLinesExistInModel(sectionLinesElemIdInSettings) && !string.IsNullOrEmpty(sectionLinesElemIdInSettings))
                {
                    SectionLinesElemIds = sectionLinesElemIdInSettings;
                    RevitModel.GetSectionLinesBySettings(sectionLinesElemIdInSettings);
                }
            }
            #endregion

            #region Команды
            GetRoadAxisCommand = new LambdaCommand(OnGetRoadAxisCommandExecuted, CanGetRoadAxisCommandExecute);
            GetSectionLinesCommand = new LambdaCommand(OnGetSectionLinesCommandExecuted, CanGetSectionLinesCommandExecute);
            AddBeamAxisCommand = new LambdaCommand(OnAddBeamAxisCommandExecuted, CanAddBeamAxisCommandExecuted);
            DeleteBeamAxisCommand = new LambdaCommand(OnDeleteBeamAxisCommandExecuted, CanDeleteBeamAxisCommandExecuted);
            CloseWindowCommand = new LambdaCommand(OnCloseWindowCommandExecuted, CanCloseWindowCommandExecute);
            #endregion
        }

        public MainWindowViewModel() { }
        #endregion
    }
}
