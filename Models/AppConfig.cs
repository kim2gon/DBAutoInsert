namespace DbAutoInsert.Models
{
    public class AppConfig
    {
        /// <summary>csv, sql, txt, db 파일 모두 이 폴더에 넣으면 자동 처리</summary>
        public string DataDirectory { get; set; } = "data";
        public string LogDirectory  { get; set; } = "logs";
    }
}
