using System;
using System.Globalization;

namespace AirLiticApp;

/// <summary>SQL-шаблони звітів (плейсхолдери дат {{RPT_DF}}, {{RPT_DT}}).</summary>
public static class ReportSql
{
    public const string DateFromPlaceholder = "{{RPT_DF}}";
    public const string DateToPlaceholder = "{{RPT_DT}}";

    public static string ApplyDates(string template, DateTime from, DateTime to)
    {
        var df = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var dt = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return template
            .Replace(DateFromPlaceholder, df, StringComparison.Ordinal)
            .Replace(DateToPlaceholder, dt, StringComparison.Ordinal);
    }

    public const string WeaponsMainTemplate = @"
declare @colls nvarchar(max);
declare @collsIsnull nvarchar(max);

select @colls = string_agg(quotename(flying_result.name), ',')
from flying_result;

-- PIVOT дає NULL без рядків для комбінації — підставляємо 0
select @collsIsnull = string_agg('isnull(' + quotename(name) + ', 0) as ' + quotename(name), ',')
from flying_result;

declare @sql nvarchar(max) = N'
select
    weaponName N''Засіб'',
    TotalHits N''Кіл-ть вильотів'',
    ' + @collsIsnull + ',
    case
        when TotalHits = 0 then 0
        else round(isnull([Уражено], 0) * 100.0 / TotalHits, 2)
    end N''KPI''
from
(
    select
        p.name as weaponName,
        r.id as ResultId,
        rs.name as ReasonName
    from results r
    left join weapon_parts wp on wp.id = r.weapon_part_id
    left join weapon p on p.id = wp.weapon_id
    left join flying_result rs on rs.id = r.flying_result_id
    where r.Date between ''{{RPT_DF}}'' and ''{{RPT_DT}}''
) src
pivot
(
    count(ResultId)
    for ReasonName in (' + @colls + ')
) p
cross apply (
    select count(*) as TotalHits
    from results rf
    left join weapon_parts rwp on rwp.id = rf.weapon_part_id
    where rwp.weapon_id = (
        select top(1) id
        from weapon
        where name = p.weaponName
    )
      and rf.Date between ''{{RPT_DF}}'' and ''{{RPT_DT}}''
) t;
';

exec sp_executesql @sql;
";

    public const string WeaponsLostTemplate = @"
declare @colls nvarchar(max);
declare @collsIsnull nvarchar(max);

select @colls = string_agg(quotename(name), ',')
from subreason_lost_drone;

select @collsIsnull = string_agg('isnull(' + quotename(name) + ', 0) as ' + quotename(name), ',')
from subreason_lost_drone;

declare @sql nvarchar(max) = N'
select
    weaponName N''Засіб'',
    TotalHits N''Кіл-ть невдалих вильотів'',
    ' + @collsIsnull + ',
    case
        when TotalHits = 0 then 0
        else round(isnull([вороже збиття], 0) * 100 / TotalHits,2)
    end N''KPI вороже збиття''  ,
    case
        when TotalHits = 0 then 0
        else round(isnull([реб противника], 0) * 100 / TotalHits,2)
    end N''KPI реб противника''   ,
    case
        when TotalHits = 0 then 0
        else round(isnull([технічні помилки], 0) * 100 / TotalHits,2)
    end N''KPI технічні помилки''  ,
    case
        when TotalHits = 0 then 0
        else round(isnull([погодні умови], 0) * 100 / TotalHits,2)
    end N''KPI погодні умови'',
    case
        when TotalHits = 0 then 0
        else round(isnull([реб свій], 0) * 100 / TotalHits,2)
    end N''KPI реб свій'',
    case
        when TotalHits = 0 then 0
        else round(isnull([помилка пілота], 0) * 100 / TotalHits,2)
    end N''KPI помилка пілота''
from
(
    select
        wp.name as weaponName,
        r.id   as ResultId,
        sld.name as ReasonName
from results r
    left join weapon_parts wpart on wpart.id = r.weapon_part_id
    left join weapon wp on wp.id = wpart.weapon_id
left join subreason_lost_drone sld on sld.id=r.subreason_lost_drone_id
where flying_result_id=2 and r.Date between ''{{RPT_DF}}'' and ''{{RPT_DT}}''
) src
pivot
(
    count(ResultId)
    for ReasonName in (' + @colls + ')
) p
cross apply (
    select
        count(*) as TotalHits
    from results rf
    left join weapon_parts rwp on rwp.id = rf.weapon_part_id
    where rwp.weapon_id = (
        select top(1) id
        from weapon
        where name = p.weaponName
    ) and flying_result_id=2
      and rf.Date between ''{{RPT_DF}}'' and ''{{RPT_DT}}''
) t;
';

exec sp_executesql @sql;
";

    public const string PilotsMainTemplate = @"
declare @colls nvarchar(max);
declare @collsIsnull nvarchar(max);

select @colls = string_agg(quotename(flying_result.name), ',')
from flying_result;

select @collsIsnull = string_agg('isnull(' + quotename(name) + ', 0) as ' + quotename(name), ',')
from flying_result;

declare @sql nvarchar(max) = N'
select
    PilotName N''Пілот'' ,
    TotalHits N''Кіл-ть вильотів'',
    ' + @collsIsnull + ',
    round(case
        when TotalHits = 0 then 0
        else (isnull([Уражено], 0) * 100.00/ TotalHits)
    end,4) N''KPI''
from
(
    select
        p.name as PilotName,
        r.id   as ResultId,
        rs.name as ReasonName
    from results r
    left join pilot         p  on p.id  = r.pilot_id
    left join flying_result rs on rs.id = r.flying_result_id
    where r.Date between ''{{RPT_DF}}'' and ''{{RPT_DT}}''
) src
pivot
(
    count(ResultId)
    for ReasonName in (' + @colls + ')
) p
cross apply (
    select
        count(*) as TotalHits
    from results rf
    where rf.pilot_id = (
        select top(1) id
        from pilot
        where name = p.PilotName
    )
      and rf.Date between ''{{RPT_DF}}'' and ''{{RPT_DT}}''
) t;
';

exec sp_executesql @sql;
";

    public const string PilotsLostTemplate = @"
declare @colls nvarchar(max);
declare @collsIsnull nvarchar(max);

select @colls = string_agg(quotename(name), ',')
from subreason_lost_drone;

select @collsIsnull = string_agg('isnull(' + quotename(name) + ', 0) as ' + quotename(name), ',')
from subreason_lost_drone;

declare @sql nvarchar(max) = N'
select
    PilotName N''Пілот'',
    TotalHits N''Кіл-ть невдалих вильотів'',
    ' + @collsIsnull + ',
    case
        when TotalHits = 0 then 0
        else round(isnull([вороже збиття], 0) * 100 / TotalHits,2)
    end N''KPI вороже збиття''  ,
    case
        when TotalHits = 0 then 0
        else round(isnull([реб противника], 0) * 100 / TotalHits,2)
    end N''KPI реб противника''   ,
    case
        when TotalHits = 0 then 0
        else round(isnull([технічні помилки], 0) * 100 / TotalHits,2)
    end N''KPI технічні помилки''  ,
    case
        when TotalHits = 0 then 0
        else round(isnull([погодні умови], 0) * 100 / TotalHits,2)
    end N''KPI погодні умови'',
    case
        when TotalHits = 0 then 0
        else round(isnull([реб свій], 0) * 100 / TotalHits,2)
    end N''KPI реб свій'',
    case
        when TotalHits = 0 then 0
        else round(isnull([помилка пілота], 0) * 100 / TotalHits,2)
    end N''KPI помилка пілота''
from
(
    select
        wp.name as PilotName,
        r.id   as ResultId,
        sld.name as ReasonName
    from results r
    left join pilot wp on wp.id = r.pilot_id
    left join subreason_lost_drone sld on sld.id = r.subreason_lost_drone_id
    where r.flying_result_id = 2 and r.Date between ''{{RPT_DF}}'' and ''{{RPT_DT}}''
) src
pivot
(
    count(ResultId)
    for ReasonName in (' + @colls + ')
) p
cross apply (
    select
        count(*) as TotalHits
    from results rf
    where rf.pilot_id = (
        select top(1) id
        from pilot
        where name = p.PilotName
    ) and rf.flying_result_id = 2
      and rf.Date between ''{{RPT_DF}}'' and ''{{RPT_DT}}''
) t;
';

exec sp_executesql @sql;
";
}
