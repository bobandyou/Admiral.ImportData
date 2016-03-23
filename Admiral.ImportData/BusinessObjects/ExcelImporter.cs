using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Spreadsheet;
using DevExpress.Xpo;

namespace Admiral.ImportData
{
    public class ExcelImporter
    {
        XafApplication _application;
        IImportOption option;
        public void Setup(XafApplication app)
        {
            _application = app;

        }

        public ExcelImporter()
        {

        }
        public void ProcessImportAction(IWorkbook document)
        {
            var os = _application.CreateObjectSpace();
            bool rst = true;
            foreach (var item in document.Worksheets)
            {
                var typeName = item.Cells[0, 1].DisplayText;
                var t = option.MainTypeInfo.Application.BOModel.GetClass(ReflectionHelper.FindType(typeName));
                var success = StartImport(item, t, os);

                item.Cells[0, 3].SetValue(success ? "����ɹ�" : "����ʧ��");

                rst &= success;

            }

            if (rst)
            {
                try
                {
                    os.CommitChanges();
                }
                catch (Exception ex)
                {
                    os.Rollback();
                }
            }
            else {
                os.Rollback();
            }
        }

        public static Action DoApplicationEvent { get; set; }

        private bool StartImport(Worksheet ws, IModelClass bo, IObjectSpace os)
        {
            //��ʼ����:
            //1.��ʹ�ñ�ͷ�ı����ҵ���������
            Dictionary<int, IModelMember> fields = new Dictionary<int, IModelMember>();
            List<SheetRowObject> objs = new List<SheetRowObject>();
            //var ws = _spreadsheet.Document.Worksheets[0];
            var columnCount = ws.Columns.LastUsedIndex;

            var updateImport = bo.TypeInfo.FindAttribute<UpdateImportAttribute>();
            var isUpdateImport = updateImport != null;
            var keyColumn = 0;
            IModelMember keyField = null;
            for (int c = 1; c <= columnCount; c++)
            {
                var fieldCaption = ws.Cells[1, c].DisplayText;
                var fieldName = bo.AllMembers.SingleOrDefault(x => x.Caption == fieldCaption);
                fields.Add(c, fieldName);
                if (isUpdateImport && fieldName.Name == updateImport.KeyMember)
                {
                    keyColumn = c;
                    keyField = fieldName;
                }
            }

            var sheetContext = new SheetContext(ws);
            var rowCount = ws.Rows.LastUsedIndex;

            for (int r = 2; r <= rowCount; r++)
            {
                //ws.Cells[r, 0].ClearContents();

                for (int c = 1; c <= columnCount; c++)
                {
                    var cel = ws.Cells[r, c];
                    if (cel.FillColor != Color.Empty)
                        cel.FillColor = Color.Empty;

                    if (cel.Font.Color != Color.Empty)
                        cel.Font.Color = Color.Empty;
                }
            }


            for (int r = 2; r <= rowCount; r++)
            {
                XPBaseObject obj;
                if (isUpdateImport)
                {
                    var cdvalue = Convert.ChangeType(ws.Cells[r, keyColumn].Value.ToObject(), keyField.Type);
                    var cri = new BinaryOperator(updateImport.KeyMember, cdvalue);
                    obj = os.FindObject(bo.TypeInfo.Type, cri) as XPBaseObject;
                    if (obj == null)
                    {
                        obj = os.CreateObject(bo.TypeInfo.Type) as XPBaseObject;
                    }
                }
                else
                {
                    obj = os.CreateObject(bo.TypeInfo.Type) as XPBaseObject;
                }

                var result = new SheetRowObject(sheetContext) {Object = obj, Row = r, RowObject = ws.Rows[r]};
                //var vle = ws.Cells[r, c];
                for (int c = 1; c <= columnCount; c++)
                {
                    var field = fields[c];
                    var cell = ws.Cells[r, c];

                    if (!cell.Value.IsEmpty)
                    {
                        object value = null;
                        //��������
                        if (typeof (XPBaseObject).IsAssignableFrom(field.MemberInfo.MemberType))
                        {
                            var conditionValue = cell.Value.ToObject();
                            //���ָ���˲�����������ֱ��ʹ��
                            var idf = field.MemberInfo.FindAttribute<ImportDefaultFilterCriteria>();
                            var condition = idf == null ? "" : idf.Criteria;

                            #region ��������

                            if (string.IsNullOrEmpty(condition))
                            {
                                //ûָ���������������������Զ����ɵģ��ض�Ϊ�ֹ�����
                                if (!field.MemberInfo.MemberTypeInfo.KeyMember.IsAutoGenerate)
                                {
                                    condition = field.MemberInfo.MemberTypeInfo.KeyMember.Name + " = ?";
                                }
                            }

                            if (string.IsNullOrEmpty(condition))
                            {
                                //����û�У���������Ψһ�����
                                var ufield =
                                    field.MemberInfo.MemberTypeInfo.Members.FirstOrDefault(
                                        x => x.FindAttribute<RuleUniqueValueAttribute>() != null
                                        );
                                if (ufield != null)
                                    condition = ufield.Name + " = ? ";
                            }

                            if (string.IsNullOrEmpty(condition))
                            {
                                //����û�У���defaultpropertyָ����
                                var ufield = field.MemberInfo.MemberTypeInfo.DefaultMember;
                                if (ufield != null)
                                {
                                    condition = ufield.Name + " = ? ";
                                }
                            }

                            #endregion

                            #region p

                            if (string.IsNullOrEmpty(condition))
                            {
                                result.AddErrorMessage(
                                    string.Format(
                                        "����û��Ϊ��������{}���ò�����������ѯ�����г����˴������޸Ĳ�ѯѯ��!",
                                        field.MemberInfo.Name), cell);
                            }
                            else
                            {
                                try
                                {
                                    var @operator = CriteriaOperator.Parse(condition, new object[] {conditionValue});
                                    var list = os.GetObjects(field.MemberInfo.MemberType, @operator, true);
                                    if (list.Count != 1)
                                    {
                                        result.AddErrorMessage(
                                            string.Format(
                                                "�����ڲ��ҡ�{0}��ʱ��ʹ�ò���������{1}��������ֵ�ǣ���{3}������ѯ�����г����˴������޸Ĳ�ѯѯ��!��������:{2}",
                                                field.MemberInfo.MemberType.FullName, condition,
                                                "�ҵ���" + list.Count + "����¼", conditionValue), cell);
                                    }
                                    else
                                    {
                                        value = list[0];
                                    }
                                }
                                catch (Exception exception1)
                                {
                                    result.AddErrorMessage(
                                        string.Format("�����ڲ��ҡ�{0}��ʱ��ʹ�ò���������{1}������ѯ�����г����˴������޸Ĳ�ѯѯ��!��������:{2}",
                                            field.MemberInfo.MemberType.FullName, condition, exception1.Message),
                                        cell);
                                }
                            }

                            #endregion

                        }
                        else if (field.MemberInfo.MemberType == typeof (DateTime))
                        {
                            if (!cell.Value.IsDateTime)
                            {
                                result.AddErrorMessage(string.Format("�ֶ�:{0},Ҫ����������!", field.Name), cell);
                            }
                            else
                            {
                                value = cell.Value.DateTimeValue;
                            }
                        }
                        else if (field.MemberInfo.MemberType == typeof (decimal) ||
                                 field.MemberInfo.MemberType == typeof (int) ||
                                 field.MemberInfo.MemberType == typeof (long) ||
                                 field.MemberInfo.MemberType == typeof (short)
                            )
                        {
                            if (!cell.Value.IsNumeric)
                            {
                                result.AddErrorMessage(string.Format("�ֶ�:{0},Ҫ����������!", field.Name), cell);
                            }
                            else
                            {
                                value = Convert.ChangeType(cell.Value.NumericValue, field.MemberInfo.MemberType);
                            }
                        }
                        else if (field.MemberInfo.MemberType == typeof (bool))
                        {
                            if (!cell.Value.IsNumeric)
                            {
                                result.AddErrorMessage(string.Format("�ֶ�:{0},Ҫ�����벼��ֵ!", field.Name), cell);
                            }
                            else
                            {
                                value = cell.Value.BooleanValue;
                            }
                        }
                        else if (field.MemberInfo.MemberType == typeof (string))
                        {
                            var v = cell.Value.ToObject();
                            if (v != null)
                                value = v.ToString();
                        }
                        else if (field.MemberInfo.MemberType.IsEnum)
                        {
                            var names = field.MemberInfo.MemberType.GetEnumNames();
                            if (names.Contains(cell.Value.TextValue))
                            {
                                value = Enum.Parse(field.MemberInfo.MemberType, cell.Value.TextValue);
                            }
                            else
                            {
                                result.AddErrorMessage(string.Format("�ֶ�:{0},����д��ö��ֵ��û�ڶ����г���!", field.Name), cell);
                            }
                        }
                        obj.SetMemberValue(field.Name, value);
                    }
                }
                objs.Add(result);

                if (DoApplicationEvent != null)
                {
                    DoApplicationEvent();

                    this.option.Progress = ((r/(decimal)rowCount));
                    //Debug.WriteLine(this.option.Progress);
                    //var progress = ws.Cells[r, 0];
                    //progress.SetValue("���");
                }
            }

            if (objs.All(x => !x.HasError)){
                try
                {
                    Validator.RuleSet.ValidateAll(os, objs.Select(x => x.Object), "Save");
                    return true;
                }
                catch (ValidationException msgs)
                {
                    var rst = true;
                    foreach (var item in msgs.Result.Results)
                    {
                        if (item.Rule.Properties.ResultType == ValidationResultType.Error && item.State == ValidationState.Invalid)
                        {
                            var r = objs.FirstOrDefault(x => x.Object == item.Target);
                            if (r != null)
                            {
                                r.AddErrorMessage(item.ErrorMessage, item.Rule.UsedProperties);
                            }
                            rst &= false;
                        }
                    }
                    return rst;
                }
            }
            return false;
        }

        IModelMember[] GetMembers(IModelClass cls)
        {
            return cls.AllMembers.Where(x =>
                !x.MemberInfo.IsAutoGenerate &&
                !x.IsCalculated &&
                !x.MemberInfo.IsReadOnly &&
                !x.MemberInfo.IsList
                ).ToArray().Except(cls.AllMembers.Where((x) =>
                {
                    var ia = x.MemberInfo.FindAttribute<ImportOptionsAttribute>();
                    return ia != null && !ia.NeedImport;
                }
                    )
                ).ToArray();
        }

        public void InitializeExcelSheet(IWorkbook book, IImportOption option)
        {
            this.option = option;
            CreateSheet(book.Worksheets[0], option.MainTypeInfo);
            if (book.Worksheets.Count == 1)
            {
                var listProperties = option.MainTypeInfo.AllMembers.Where(x => x.MemberInfo.IsList && x.MemberInfo.ListElementTypeInfo.IsPersistent);
                foreach (var item in listProperties)
                {
                    var cls = option.MainTypeInfo.Application.BOModel.GetClass(item.MemberInfo.ListElementTypeInfo.Type);
                    var b = book.Worksheets.Add(cls.Caption);
                    CreateSheet(b, cls);
                }
            }
        }

        private void CreateSheet(Worksheet book, IModelClass boInfo)
        {
            book.Name = boInfo.Caption;
            book.Cells[0, 0].Value = "ϵͳ����";
            book.Cells[0, 1].Value = boInfo.TypeInfo.FullName;
            book.Cells[0, 2].Value = "������ϢΪ����ʱ��Ӧϵͳҵ����Ϣ������ɾ��!";
            //1.��һ�У�������ʾ��Ϣ.
            //2.��һ�У�������ʾ¼�������Ϣ��
            var i = 1;
            #region main
            var cells = book.Cells;
            var members = GetMembers(boInfo);
            foreach (var item in members)
            {
                var c = cells[1, i];
                c.Value = item.Caption;
                c.FillColor = Color.FromArgb(255, 153, 0);
                c.Font.Color = Color.White;
                var isRequiredField = IsRequiredField(item);

                var range = book.Range.FromLTRB(i, 2, i, 20000);

                //DataValidation dv = null;

                if (isRequiredField)
                {
                    c.Font.Bold = true;
                }
                i++;
            }
            #endregion
        }

        IRule[] _rules;
        IRule[] Rules
        {
            get
            {
                if (_rules == null)
                {
                    _rules = Validator.RuleSet.GetRules().ToArray();
                }
                return _rules;
            }
        }

        public bool IsRequiredField(IModelMember member)
        {
            //Rules.Where(x=>x.t)
            return Rules.Any(x => x.Properties is IRuleRequiredFieldProperties && x.Properties.TargetType == member.ModelClass.TypeInfo.Type && x.UsedProperties.IndexOf(member.Name) > -1);

        }
    }
}