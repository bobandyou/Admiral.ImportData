using System;

namespace Admiral.ImportData
{
    /// <summary>
    /// ָʾ���ԡ��ֶ��Ƿ���Ҫ����
    /// </summary>
    [AttributeUsage(AttributeTargets.Property| AttributeTargets.Field )]
    public class ImportOptionsAttribute : Attribute
    {
        public bool NeedImport { get;private set; } 

        public ImportOptionsAttribute(bool needImport)
        {
            this.NeedImport = needImport;
        }
    }
}