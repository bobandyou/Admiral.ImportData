using System;

namespace Admiral.ImportData
{
    /// <summary>
    /// �ڵ�������ʱ��ָ���������������ȥ����ֵ
    /// ��ζ����ǣ���Name=?����ʹ��Excel�е�Ԫ���ֵȥ�滻���ŵ�ֵ��
    /// �нṹ���ͻ������������䡢��ϵ�ˣ���ϵ���������ֻ��������ڵ���ͻ���Ϣʱ����ϵ�������������ԣ�ָ���� ����ϵ��������������Ե��롣
    /// ָ��������Ϊ ����ϵ������������[ImportDefaultFilterCriteria("��ϵ������=?")]���ɡ�
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class ImportDefaultFilterCriteria : Attribute
    {
        // Methods
        public ImportDefaultFilterCriteria(string criteria)
        {
            this.Criteria = criteria;
        }

        // Properties
        public string Criteria { get; set; }
    }

    
}