CREATE TABLE IF NOT EXISTS TrustedPeer(
  CertificateThumbprint TEXT NOT NULL PRIMARY KEY,
  DeviceId TEXT NOT NULL,
  DeviceName TEXT NOT NULL,
  AddedUtc TEXT NOT NULL
);
