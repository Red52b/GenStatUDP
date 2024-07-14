using System;
using System.IO;
using System.Xml.Serialization;

namespace udp_server
{
		public static class SettingsXmlHelper
		{
			const string fileName = "server_config.xml";
			
			public static void Save(SetingsStruct mySettings){
				XmlSerializer serializer = new XmlSerializer(typeof(SetingsStruct));
				TextWriter writer = new StreamWriter(fileName);
				mySettings.Connection.CastGroup="235.35.35.0";

				serializer.Serialize(writer, mySettings);
				writer.Close();
			}
			
			public static SetingsStruct Load(){
				FileStream fs = null;

				try{
					fs = new FileStream(fileName, FileMode.Open);
					XmlSerializer serializer = new XmlSerializer(typeof(SetingsStruct));

					return (SetingsStruct) serializer.Deserialize(fs);
				}
				catch (FileNotFoundException ex){
					Console.WriteLine("Файл настроек не найден. Проверьте наличие '"+fileName+"' в папке прогрммы");
					if(ex != null){		ex = null;		}	// затычка чтоб Warning`а не было
				}
				catch (Exception ex){
					Console.WriteLine(ex.Message);
				}
				finally{
					//	fs?.Close();	// ?. еще не добавили
					if(fs!=null){
						fs.Close();
					}
				}

				return new SetingsStruct();
			}
			
			public static bool Check(SetingsStruct mySettings){
				if(mySettings.Connection.CastGroup==null || mySettings.Connection.CastGroup.Length==0){
					Console.WriteLine("Не удалось загрузить файл параметров");
					return false;
				}
				if(mySettings.Connection.Port==0){
					Console.WriteLine("Не указан 'Port' подключения");
					return false;
				}
				if(mySettings.StatCore.ValueDigits <= 0){
					Console.WriteLine("Параметр 'ValueDigits' должен быть больше нуля");
					return false;
				}
				if(mySettings.StatCore.RndParam < 10){
					mySettings.StatCore.RndParam = 10;
					Console.WriteLine("Параметр 'RndParam' должен быть не меньше 10. Спользуем 'RndParam' = 10");
				}
				return true;
			}
		}
}