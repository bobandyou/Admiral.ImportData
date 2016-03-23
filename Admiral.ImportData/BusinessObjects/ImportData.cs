using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Admiral.ImportData
{
    [XafDisplayName("���ݵ���")]
    [NonPersistent]
    [ImageName("ImportData")]
    public class ImportData : BaseObject
    {
        public ImportData(Session s) : base(s)
        {

        }

        public IImportOption Option { get; set; }

        private decimal _Progress;
        [XafDisplayName("����")][ModelDefault("AllowEdit","False")]
        public decimal Progress
        {
            get { return _Progress; }
            set { SetPropertyValue("Progress", ref _Progress, value); }
        }

    }
}