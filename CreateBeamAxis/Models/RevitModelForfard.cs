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
using BridgeDeck.Models;
using CreateBeamAxis.Models;
using CreateBeamAxis.Infrastructure;

namespace CreateBeamAxis
{
    public class RevitModelForfard
    {
        private UIApplication Uiapp { get; set; } = null;
        private Application App { get; set; } = null;
        private UIDocument Uidoc { get; set; } = null;
        private Document Doc { get; set; } = null;

        public RevitModelForfard(UIApplication uiapp)
        {
            Uiapp = uiapp;
            App = uiapp.Application;
            Uidoc = uiapp.ActiveUIDocument;
            Doc = uiapp.ActiveUIDocument.Document;
        }

        #region Проверка на то существуют линии оси и линии на поверхности в модели
        public bool IsLinesExistInModel(string elemIdsInSettings)
        {
            var elemIds = RevitGeometryUtils.GetIdsByString(elemIdsInSettings);

            return RevitGeometryUtils.IsElemsExistInModel(Doc, elemIds, typeof(DirectShape));
        }
        #endregion

        #region Ось трассы
        public PolyCurve RoadAxis { get; set; }

        private string _roadAxisElemIds;

        public string RoadAxisElemIds
        {
            get => _roadAxisElemIds;
            set => _roadAxisElemIds = value;
        }

        public void GetPolyCurve()
        {
            var curves = RevitGeometryUtils.GetCurvesByRectangle(Uiapp, out _roadAxisElemIds);
            RoadAxis = new PolyCurve(curves);
        }
        #endregion

        #region Получение оси трассы из Settings
        public void GetAxisBySettings(string elemIdsInSettings)
        {
            var elemIds = RevitGeometryUtils.GetIdsByString(elemIdsInSettings);
            var lines = RevitGeometryUtils.GetCurvesById(Doc, elemIds);
            RoadAxis = new PolyCurve(lines);
        }
        #endregion

        #region Плоскость для построения линий
        private SketchPlane _sketchPlane;
        #endregion

        #region Линии границ блоков
        public List<Line> SectionLines { get; set; }

        private string _sectionLinesElemIds;
        public string SectionLinesElemIds
        {
            get => _sectionLinesElemIds;
            set => _sectionLinesElemIds = value;
        }

        public void GetSectionLines()
        {
            SectionLines = RevitGeometryUtils.GetCurvesByLines(Uiapp, out _sectionLinesElemIds, out _sketchPlane);
        }

        public void GetSectionLinesBySettings(string elemIdsInSettings)
        {
            var elemIds = RevitGeometryUtils.GetIdsByString(elemIdsInSettings);
            SectionLines = RevitGeometryUtils.GetProfileLinesById(Doc, elemIds, out _sketchPlane);
        }
        #endregion

        #region Проверка на то существуют линии для построения профилей
        public bool IsProfileLinesExistInModel(string elemIdsInSettings)
        {
            var elemIds = RevitGeometryUtils.GetIdsByString(elemIdsInSettings);

            return RevitGeometryUtils.IsElemsExistInModel(Doc, elemIds, typeof(ModelLine));
        }
        #endregion

        #region Создание осей блоков ПС
        public void CreateBeamAxis(IEnumerable<BeamAxis> beamParameters)
        {
            var parameters = new List<(Line SectionLine, double Parameter)>(SectionLines.Count);

            foreach (var sectionLine in SectionLines)
            {
                double parameter;
                if (RoadAxis.Intersect(sectionLine, out _, out parameter))
                {
                    parameters.Add((sectionLine, parameter));
                }
            }

            parameters = parameters.OrderBy(p => p.Parameter).ToList();

            var paramsForBeamAxisCreating = new List<(XYZ StartPoint, XYZ AxisDirection, XYZ RoadDirection)>();

            for (int i = 0; i < parameters.Count - 1; i++)
            {
                XYZ point = RoadAxis.GetPointOnPolycurve(parameters.ElementAt(i).Parameter, out _);

                var sectionAxisDirection = parameters.ElementAt(i).SectionLine.Direction.Normalize();

                XYZ roadAxisPoint2 = RoadAxis.GetPointOnPolycurve(parameters.ElementAt(i + 1).Parameter, out _);

                Line roadAxisLine = Line.CreateBound(point, roadAxisPoint2);

                var roadAxisDirection = roadAxisLine.Direction;

                XYZ normalVector = sectionAxisDirection.CrossProduct(roadAxisDirection);
                if (normalVector.Z < 0)
                {
                    sectionAxisDirection = sectionAxisDirection.Negate();
                }

                paramsForBeamAxisCreating.Add((point, sectionAxisDirection, roadAxisDirection));
            }

            XYZ lastPoint = RoadAxis.GetPointOnPolycurve(parameters.Last().Parameter, out _);
            XYZ lastPointDirection = parameters.Last().SectionLine.Direction.Normalize();
            XYZ lastRoadAxisLineDirection = paramsForBeamAxisCreating.Last().RoadDirection;
            XYZ lastNormal = lastPointDirection.CrossProduct(lastRoadAxisLineDirection);
            if (lastNormal.Z < 0)
            {
                lastPointDirection = lastPointDirection.Negate();
            }

            paramsForBeamAxisCreating.Add((lastPoint, lastPointDirection, lastRoadAxisLineDirection));

            var beamAxis = new List<Line>();

            for (int i = 0; i < parameters.Count - 1; i++)
            {
                foreach (var blockParam in beamParameters)
                {
                    double distance = UnitUtils.ConvertToInternalUnits(blockParam.Distance, UnitTypeId.Meters);

                    XYZ startPoint1 = paramsForBeamAxisCreating.ElementAt(i).StartPoint;
                    XYZ startPoint2 = paramsForBeamAxisCreating.ElementAt(i + 1).StartPoint;

                    XYZ vector1 = paramsForBeamAxisCreating.ElementAt(i).AxisDirection * distance;
                    XYZ vector2 = paramsForBeamAxisCreating.ElementAt(i + 1).AxisDirection * distance;

                    XYZ point1 = startPoint1 + vector1;
                    XYZ point2 = startPoint2 + vector2;

                    Line axis = Line.CreateBound(point1, point2);

                    beamAxis.Add(axis);
                }
            }

            using(Transaction trans = new Transaction(Doc, "Created Beam Axis"))
            {
                trans.Start();
                bool isFamily = Doc.IsFamilyDocument;
                foreach(var line in beamAxis)
                {
                    if(isFamily)
                    {
                        ModelCurve modelCurve = Doc.FamilyCreate.NewModelCurve(line, _sketchPlane);
                    }
                    else
                    {
                        ModelCurve modelCurve = Doc.Create.NewModelCurve(line, _sketchPlane);
                    }
                }
                trans.Commit();
            }
        }
        #endregion

    }
}
