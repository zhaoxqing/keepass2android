using System.IO;
using System.Xml.Serialization;
using KeePassLib.Serialization;
using OtpKeyProv;
using keepass2android.Io;

namespace keepass2android.addons.OtpKeyProv
{
	/// <summary>
	/// Class which provides caching for OtpInfo-files. This is an extension to CachingFileStorage required to handle conflicts directly when loading.
	/// </summary>
	class OtpAuxCachingFileStorage: CachingFileStorage
	{
		private readonly IOtpAuxCacheSupervisor _cacheSupervisor;

		internal interface IOtpAuxCacheSupervisor: ICacheSupervisor
		{
			/// <summary>
			/// called when there was a conflict which was resolved by using the remote file.
			/// </summary>
			void ResolvedCacheConflictByUsingRemote(IOConnectionInfo ioc);

			/// <summary>
			/// called when there was a conflict which was resolved by using the local file.
			/// </summary>
			void ResolvedCacheConflictByUsingLocal(IOConnectionInfo ioc);
		}


		public OtpAuxCachingFileStorage(IFileStorage cachedStorage, string cacheDir, IOtpAuxCacheSupervisor cacheSupervisor)
			: base(cachedStorage, cacheDir, cacheSupervisor)
		{
			_cacheSupervisor = cacheSupervisor;
		}

		protected override Stream OpenFileForReadWithConflict(IOConnectionInfo ioc, string cachedFilePath)
		{
			OtpInfo remoteOtpInfo, localOtpInfo;
			//load both files
			XmlSerializer xs = new XmlSerializer(typeof (OtpInfo));
			localOtpInfo = (OtpInfo) xs.Deserialize(File.OpenRead(cachedFilePath));
			using (Stream remoteStream = _cachedStorage.OpenFileForRead(ioc))
			{
				remoteOtpInfo = (OtpInfo) xs.Deserialize(remoteStream);
			}

			//see which OtpInfo has the bigger Counter value and use this one:
			if (localOtpInfo.Counter > remoteOtpInfo.Counter)
			{
				//overwrite the remote file
				UpdateRemoteFile(File.OpenRead(cachedFilePath), 
				                 ioc, 
				                 App.Kp2a.GetBooleanPreference(PreferenceKey.UseFileTransactions),
				                 GetBaseVersionHash(ioc)
					);

				_cacheSupervisor.ResolvedCacheConflictByUsingRemote(ioc);
			}
			else
			{
				//overwrite the local file:
				UpdateCacheFromRemote(ioc, cachedFilePath);
				_cacheSupervisor.ResolvedCacheConflictByUsingLocal(ioc);
			}

			//now return the local file in any way:
			return File.OpenRead(cachedFilePath);
		}
	}
}