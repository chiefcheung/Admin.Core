using ZhonTai.Common.Domain.Entities;
using FreeSql.DataAnnotations;

namespace ZhonTai.Plate.Admin.Domain.DictionaryType
{
    /// <summary>
    /// 数据字典类型
    /// </summary>
	[Table(Name = "ad_dictionary_type")]
    [Index("idx_{tablename}_01", nameof(Name) + "," + nameof(TenantId), true)]
    public class DictionaryTypeEntity : EntityFull, ITenant
    {
        /// <summary>
        /// 租户Id
        /// </summary>
        [Column(Position = -10, CanUpdate = false)]
        public long? TenantId { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        [Column(StringLength = 50)]
        public string Name { get; set; }

        /// <summary>
        /// 编码
        /// </summary>
        [Column(StringLength = 50)]
        public string Code { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        [Column(StringLength = 500)]
        public string Description { get; set; }

        /// <summary>
        /// 启用
        /// </summary>
		public bool Enabled { get; set; } = true;

        /// <summary>
        /// 排序
        /// </summary>
		public int Sort { get; set; }
    }
}