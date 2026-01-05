namespace Alga.sessions.Models;
public class ValueModel
{
    public required string Token { get; set; } // session token
    public required string TokenHidden { get; set; } // session token hidden
    public DateTime Dt { get; set; } = DateTime.UtcNow; // время создания или время последнего рефреша
    public long NumberOfErrors { get; set; } = 0; // количество ошибочных попыток входа
    //public byte ToLog { get; set; } = 1; // добавить в log File если он существует. Where: 1 - обновить / 2 удалить
}

// В теории мы можем соединить TokenHidden + Token