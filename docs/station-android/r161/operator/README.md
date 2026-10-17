# Política recebida R161

`received_policy.py` é a adaptação pura usada pelo operador privado. Ela aceita
somente os quatro owners exatos recebidos e o perfil Android Dynarec ef8bfd6e,
reproduz o incoming/policies do kit e restringe a diferença de opções ao CPU N64.
Não modifica o builder histórico R128 nem aprova jogos. A publicação usa o
registrador protegido vigente, outra baseline para cada transação e scan.lock.

As provas em `../evidence` registram preflight, bytes reais, assinatura, fonte do
operador e aplicação. O preflight isolado usa baseline histórica e não publica.
Só `server-applied.json` comprova a aplicação. Não executar novamente.

O [manual oficial do core](https://github.com/mupen64plus/mupen64plus-core/blob/master/doc/emuwiki-api-doc/Mupen64Plus-Core-Parameters.mediawiki)
documenta os modos CPU1 e CPU2. O [protocolo oficial](https://github.com/mupen64plus/mupen64plus-core/blob/master/doc/emuwiki-api-doc/Mupen64Plus-v2.0-Netplay-API.mediawiki)
sincroniza emumode nas configurações. Esses contratos não comprovam desempenho
ou determinismo desta partida no Android; os dois aparelhos precisam do mesmo
APK/perfil e de teste físico. A R161 continua candidata até essa validação.
