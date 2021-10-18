using ZhonTai.Plate.Admin.Domain.Permission;

namespace ZhonTai.Plate.Admin.Service.Permission.Input
{
    public class PermissionAddGroupInput
    {
        /// <summary>
        /// Ȩ������
        /// </summary>
        public PermissionTypeEnum Type { get; set; }

        /// <summary>
        /// �����ڵ�
        /// </summary>
        public long ParentId { get; set; }

        /// <summary>
        /// Ȩ������
        /// </summary>
        public string Label { get; set; }

        ///// <summary>
        ///// ˵��
        ///// </summary>
        //public string Description { get; set; }

        /// <summary>
        /// ����
        /// </summary>
		public bool Hidden { get; set; }

        ///// <summary>
        ///// ����
        ///// </summary>
        //public bool Enabled { get; set; }

        /// <summary>
        /// ͼ��
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// ��
        /// </summary>
        public bool? Opened { get; set; }
    }
}