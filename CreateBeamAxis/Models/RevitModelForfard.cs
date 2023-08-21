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

            var paramsForBeamAxisCreating = new List<(Line AxisLine1, Line AxisLine2, Line RoadLine, XYZ ReferenceVector)>();

            for(int i = 0; i < parameters.Count - 1; i++)
            {
                XYZ lineOrigin1 = RoadAxis.GetPointOnPolycurve(parameters.ElementAt(i).Parameter, out _);
                XYZ lineDirection1 = parameters.ElementAt(i).SectionLine.Direction;
                Line axisLine1 = Line.CreateUnbound(lineOrigin1, lineDirection1);

                XYZ lineOrigin2 = RoadAxis.GetPointOnPolycurve(parameters.ElementAt(i + 1).Parameter, out _);
                XYZ lineDirection2 = parameters.ElementAt(i + 1).SectionLine.Direction;
                Line axisLine2 = Line.CreateUnbound(lineOrigin2, lineDirection2);

                XYZ roadLineDirection = lineOrigin1 - lineOrigin2;
                Line roadLine = Line.CreateBound(lineOrigin1, lineOrigin2);

                XYZ referenceVector = roadLineDirection.CrossProduct(XYZ.BasisZ).Normalize();

                paramsForBeamAxisCreating.Add((axisLine1, axisLine2, roadLine, referenceVector));
            }

            var beamAxis = new List<Line>();

            beamParameters = beamParameters.Append(new BeamAxis { Distance = 0 });

            foreach(var param in paramsForBeamAxisCreating)
            {
                foreach(var axisParam in beamParameters)
                {
                    double distance = UnitUtils.ConvertToInternalUnits(axisParam.Distance, UnitTypeId.Meters);

                    XYZ offsetPoint1 = param.RoadLine.GetEndPoint(0) + param.ReferenceVector * distance;
                    XYZ offsetPoint2 = param.RoadLine.GetEndPoint(1) + param.ReferenceVector * distance;

                    Line offsetLine = Line.CreateBound(offsetPoint1, offsetPoint2);

                    Line unboundOffsetLine = Line.CreateUnbound(offsetLine.GetEndPoint(0), offsetLine.Direction);

                    XYZ point1 = null;
                    var result1 = new IntersectionResultArray();
                    var compResult1 = unboundOffsetLine.Intersect(param.AxisLine1, out result1);
                    if (compResult1 == SetComparisonResult.Overlap)
                    {
                        foreach(var elem in result1)
                        {
                            if(elem is IntersectionResult interResult)
                            {
                                point1 = interResult.XYZPoint;
                            }
                        }
                    }

                    XYZ point2 = null;
                    var result2 = new IntersectionResultArray();
                    var compResult2 = unboundOffsetLine.Intersect(param.AxisLine2, out result2);
                    if(compResult2 == SetComparisonResult.Overlap)
                    {
                        foreach(var elem in result2)
                        {
                            if(elem is IntersectionResult interResult)
                            {
                                point2 = interResult.XYZPoint;
                            }    
                        }
                    }

                    if (!(point1 is null || point2 is null))
                    {
                        beamAxis.Add(Line.CreateBound(point1, point2));
                    }
                }
            }

            using(Transaction trans = new Transaction(Doc, "Created Beam Axis"))
            {
                trans.Start();
                bool isFamilyDoc = Doc.IsFamilyDocument;
                foreach(var line in beamAxis)
                {
                    if(isFamilyDoc)
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
