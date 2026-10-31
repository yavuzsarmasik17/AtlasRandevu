using System;

namespace AtlasRandevu.Domain.Entities
{
    public class SubeCalismaSaati
    {
        public int Id { get; set; }
        public int SubeId { get; set; }
        // .NET DayOfWeek: Pazar=0, Pazartesi=1, ... Cumartesi=6
        public DayOfWeek Gun { get; set; }
        public bool AcikMi { get; set; }
        public TimeSpan Acilis { get; set; } = new TimeSpan(9, 0, 0);
        public TimeSpan Kapanis { get; set; } = new TimeSpan(18, 0, 0);
        public Sube Sube { get; set; } = null!;
    }
}
