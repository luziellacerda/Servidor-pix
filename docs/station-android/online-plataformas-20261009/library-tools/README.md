# Importador Station: publicação PS2/Saturn de 10/10/2026

Fonte completa da release ativa; o mesmo handoff contém o recibo atual. Não repetir operadores datados ou levar a configuração privada para o APK.

SNES usa `catalogSeed:station-catalog-seed.json`. Saturn usa `artifactMode:cue-disc`, `copyRawOnce:true`, `rawStorage:readonly-hardlink`, `normalizeDiscArchives:true` e `artifactDirectory` no mesmo volume das ROMs. PS2 e Switch mantêm seus arquivos raw. A unidade root precisa escrever nas origens congeladas; a API lê os artefatos protegidos. CHD estável passa a root/0444 e usa um inode, sem copiar o corpo. Trocar ROM significa substituir o nome por um arquivo novo.

Saturn ZIP/7z/RAR deve conter um CUE único, ou declarar `launchPath`, e todas as faixas. O servidor extrai uma vez durante a importação, prepara ZIP completo com compressão DEFLATE, identidade `cue-set-v1` e caminhos corretos. O original é preservado. A extração e os hashes não são repetidos em cada download. Arquivo incompleto permanece pendente.

Publicação requer duas observações estáveis e idade mínima de 20 segundos. Capas exatas ficam em `media/revista`; o seed fornece associações por título normalizado. XML e overrides têm prioridade. JPEG RGB 480×720 conforme é preservado byte a byte. Perfis anteriores são preservados, inclusive recusas reais. Os testes usam arquivos sintéticos e não comprovam emulação.

`cadastrar-motor-online-station.py install` aceita os 17 sistemas, verifica core/runtime reais e configurações explícitas dos controles; publicação exige administração local e recarrega por dados, sem recompilar/reiniciar o servidor. Registros de identidades, perfis e modos da sombra devem estar dentro do seu outputDirectory.
