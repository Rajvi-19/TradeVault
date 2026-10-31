namespace TradeVault.Desktop;
public static class ExcelFields
{
 public static IReadOnlyList<Field> For(string name) => name switch
 {
 "form" => [
new("F1","Party name",false,150,Locked:true),
new("F2","Country name",false,115,Locked:true),
new("F27","Agent name",false,155,Locked:true),
new("F3","Proforma invoice number",false,155,Locked:true),
new("F4","Proforma invoice date",false,135,Locked:true),
new("F5","Original amount ($)",false,240),
new("F6","Our customs invoice amount ($)",false,240),
new("F7","Pakistan customs invoice amount ($)",false,240),
new("F8","Difference amount ($)",true,240),
new("F9","India invoice number",false,240),
new("F10","India invoice date",false,240),
new("F11","Container number from India",false,240),
new("F12","India BL number",false,240),
new("F13","India BL date",false,240),
new("F14","Agent country arrival date",false,240),
new("F15","Container number from agent country",false,240),
new("F16","Agent country BL number",false,240),
new("F17","Agent country BL date",false,240),
new("F19","Payment due date",false,240),
new("F20","Reminder 30 days before due date",true,240),
new("F21","Overdue days",true,240),
],
 "details" => [
new("A","SR. NO.",true,240),
new("B","OUR CUSTOMS INVOICE NO & DATE",false,240),
new("C","ORIGINAL AMOUNT $",false,240),
new("D","BANK CONTRACT AMOUNT $",false,240),
new("E","REC. INVOICE  PAYMENT IN MALAYSIA A/C",false,240),
new("F","PENDING INVOICE PAYMENT IN MALAYSIA A/C",true,240),
new("G","DIFFRENCE AMAOUNT $",true,240),
new("I","PER BILL DIFFERENCE USD",true,240),
new("J","REC. DIFF. AMOUNT",false,240),
new("K","TOTAL DIFF. AMOUNT PENDING $",true,240),
new("L","BL DATE. MLY TO PAK",false,240),
new("M","TERMS",false,240),
new("N","DUE DATE",true,240),
],
 "ledger" => [
new("B","Transaction description",false,240),
new("C","Amount added / USD (Debit)",false,240),
new("D","Amount deducted / USD (Credit)",false,240),
new("E","Running amount owed / USD",true,240),
new("G","Payment due date",true,240),
new("H","Bank amount / USD",false,240),
new("I","Bill of lading date",false,240),
new("J","Payment terms / days",false,240),
new("K","Payment due date",true,240),
],
 _ => []
 };
}
