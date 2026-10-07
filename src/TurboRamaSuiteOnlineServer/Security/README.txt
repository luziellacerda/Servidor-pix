Official public Android attestation trust anchors, downloaded 2026-10-07 from
https://android.googleapis.com/attestation/root
Bundle SHA256 add931656106e4f32dcfc29aca0d36cb71dc37f9c68ae4803314762a64ef6f17
Validation requirements:
https://developer.android.com/privacy-and-security/security-key-attestation
ASN.1 schema:
https://source.android.com/docs/security/features/keystore/attestation
No private key is included. Trust anchors cannot be replaced by API input or
production environment configuration. Old factory chains with expired batch
certificates are intentionally not accepted as verified; use the compatible
RSA request proof mode until their hardware compatibility is qualified.
