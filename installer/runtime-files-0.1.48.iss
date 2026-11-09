{ .NET 8 runtime files shipped in Slithy 0.1.48. Leave files included in the new payload alone. }
#if !FileExists(PublishDir + "\cs\PresentationCore.resources.dll")
RemovePriorRuntimeFile('cs\PresentationCore.resources.dll', '46cd294204420afb5a1d05f3a780b0670e01608b0ecc3886ac0ec0abd9020e83');
#endif
#if !FileExists(PublishDir + "\cs\PresentationFramework.resources.dll")
RemovePriorRuntimeFile('cs\PresentationFramework.resources.dll', 'b8d2024511564a0ab7053cfeb79a3fe79d63124c60c6a259a59345418ca9a011');
#endif
#if !FileExists(PublishDir + "\cs\PresentationUI.resources.dll")
RemovePriorRuntimeFile('cs\PresentationUI.resources.dll', '5ddf69f382eec8bd1bc6ef7ab5cdc2ef1ad936d2ad7c38292efc0454370f134d');
#endif
#if !FileExists(PublishDir + "\cs\ReachFramework.resources.dll")
RemovePriorRuntimeFile('cs\ReachFramework.resources.dll', '00542acf04b249a8ba870e6cf9356ecba5d8ab30ae5a6ac91a533fb553769804');
#endif
#if !FileExists(PublishDir + "\cs\System.Windows.Controls.Ribbon.resources.dll")
RemovePriorRuntimeFile('cs\System.Windows.Controls.Ribbon.resources.dll', 'f8906033f10c1b9ae714106a70e40d0b80ceeb0566496b58f979e58e5d789344');
#endif
#if !FileExists(PublishDir + "\cs\System.Windows.Input.Manipulations.resources.dll")
RemovePriorRuntimeFile('cs\System.Windows.Input.Manipulations.resources.dll', 'd0ba3794951eef376f49002e4de3dfa5663de145314921286ac9338d04fa97b8');
#endif
#if !FileExists(PublishDir + "\cs\System.Xaml.resources.dll")
RemovePriorRuntimeFile('cs\System.Xaml.resources.dll', '557d718fe29f7ce7e70b2fb30a646424c8d04ba869d62360dbc6c5513568c558');
#endif
#if !FileExists(PublishDir + "\cs\UIAutomationClient.resources.dll")
RemovePriorRuntimeFile('cs\UIAutomationClient.resources.dll', '5dd1aae2240aeceacc93d0af11f506299e0fc6e0829df7310584810a947d3f5d');
#endif
#if !FileExists(PublishDir + "\cs\UIAutomationClientSideProviders.resources.dll")
RemovePriorRuntimeFile('cs\UIAutomationClientSideProviders.resources.dll', '789866f3ec7ed8d953c9c5afac03c09337fc1980257ce158fa181609a939202b');
#endif
#if !FileExists(PublishDir + "\cs\UIAutomationProvider.resources.dll")
RemovePriorRuntimeFile('cs\UIAutomationProvider.resources.dll', 'd82ee589909d285f1f6dd373e4a85352f811d685b23d222770a6c430503c7a29');
#endif
#if !FileExists(PublishDir + "\cs\UIAutomationTypes.resources.dll")
RemovePriorRuntimeFile('cs\UIAutomationTypes.resources.dll', 'eeb0625046091974b596234a8d423b22d30eb6eeedb4aeed1ca112b179bea2a4');
#endif
#if !FileExists(PublishDir + "\cs\WindowsBase.resources.dll")
RemovePriorRuntimeFile('cs\WindowsBase.resources.dll', 'f938cb16d0bca68b6e896f96e9ea46f9d6c9dac1f890d9d4b8a617db7f356a49');
#endif
#if !FileExists(PublishDir + "\cs\WindowsFormsIntegration.resources.dll")
RemovePriorRuntimeFile('cs\WindowsFormsIntegration.resources.dll', '2f5ff15dafc7abfdd821393b6135d7d52bed228f634ea25fb144a8bc19033063');
#endif
#if !FileExists(PublishDir + "\D3DCompiler_47_cor3.dll")
RemovePriorRuntimeFile('D3DCompiler_47_cor3.dll', 'a05f99734f7c4822fefc12b367af21fd0976ed6608752fb1e1e80b6ece7ecbbb');
#endif
#if !FileExists(PublishDir + "\de\PresentationCore.resources.dll")
RemovePriorRuntimeFile('de\PresentationCore.resources.dll', 'c1cc2e7b8d7f36c35b0451cb701937301becc2a404e2838d3af8ebe15b77b43c');
#endif
#if !FileExists(PublishDir + "\de\PresentationFramework.resources.dll")
RemovePriorRuntimeFile('de\PresentationFramework.resources.dll', '1cb7adeccd6cb2f07e0ef9bf418cd03a45fe61bfadaf4e6e9b170b4f7918bca8');
#endif
#if !FileExists(PublishDir + "\de\PresentationUI.resources.dll")
RemovePriorRuntimeFile('de\PresentationUI.resources.dll', '22d033639785630797edabbb401cf7681ee0a51d401fe6230d6538cb26fd4509');
#endif
#if !FileExists(PublishDir + "\de\ReachFramework.resources.dll")
RemovePriorRuntimeFile('de\ReachFramework.resources.dll', '197e1f296b4d60fe05c07157a7bd6235a0e782323281e692e94a1ba205f5b623');
#endif
#if !FileExists(PublishDir + "\de\System.Windows.Controls.Ribbon.resources.dll")
RemovePriorRuntimeFile('de\System.Windows.Controls.Ribbon.resources.dll', '4cd92c3db9ee177f8670d0259802d4875d3793ab57d47aa48f8970a17be8c1e6');
#endif
#if !FileExists(PublishDir + "\de\System.Windows.Input.Manipulations.resources.dll")
RemovePriorRuntimeFile('de\System.Windows.Input.Manipulations.resources.dll', '3d82dd1900284e3a929e2f5db89cf945dce7bbbed404d3731c37d53ab98ac7aa');
#endif
#if !FileExists(PublishDir + "\de\System.Xaml.resources.dll")
RemovePriorRuntimeFile('de\System.Xaml.resources.dll', '9902c165f0b66e88f26688d51f4fb709419fbd62196f226d870ca95396a650dd');
#endif
#if !FileExists(PublishDir + "\de\UIAutomationClient.resources.dll")
RemovePriorRuntimeFile('de\UIAutomationClient.resources.dll', 'ee66a5dfa117c2e140b8b9784fda9bc10e560210c07f47d7b3b4223263859477');
#endif
#if !FileExists(PublishDir + "\de\UIAutomationClientSideProviders.resources.dll")
RemovePriorRuntimeFile('de\UIAutomationClientSideProviders.resources.dll', '341e626775ac8632073053cf96bbf35340e77bb92a83a9e02a5a88154583bf03');
#endif
#if !FileExists(PublishDir + "\de\UIAutomationProvider.resources.dll")
RemovePriorRuntimeFile('de\UIAutomationProvider.resources.dll', 'f83b0b3f0759fdab9b280051b82123ead7fc2ee21ffba2ad5f8d92c329601977');
#endif
#if !FileExists(PublishDir + "\de\UIAutomationTypes.resources.dll")
RemovePriorRuntimeFile('de\UIAutomationTypes.resources.dll', '9fbc40a043f2e9494c87d1d3150ffaf6bfa9d7256affae15eacbb05e813e846e');
#endif
#if !FileExists(PublishDir + "\de\WindowsBase.resources.dll")
RemovePriorRuntimeFile('de\WindowsBase.resources.dll', 'a721e299fc5c2072929a0fa24dc9c3e0631d9919b12f02f1674539ab799c1aa4');
#endif
#if !FileExists(PublishDir + "\de\WindowsFormsIntegration.resources.dll")
RemovePriorRuntimeFile('de\WindowsFormsIntegration.resources.dll', '09592b0f73723c785e13185ed6b5a1e0136245b4ff2db057205ff2052fdbf06b');
#endif
#if !FileExists(PublishDir + "\DirectWriteForwarder.dll")
RemovePriorRuntimeFile('DirectWriteForwarder.dll', 'b519c3667b3f7fc9f1b5d8cf8cb48ef6b5ad43ae74925480ef9ff9a50bebf7cf');
#endif
#if !FileExists(PublishDir + "\es\PresentationCore.resources.dll")
RemovePriorRuntimeFile('es\PresentationCore.resources.dll', '05211326bb3f006298c28eeb5d14696c9e172250a80b7acfdf41d457d9fd1eec');
#endif
#if !FileExists(PublishDir + "\es\PresentationFramework.resources.dll")
RemovePriorRuntimeFile('es\PresentationFramework.resources.dll', '7faee4fc18a48d0bf142dec3d499ee933202e0ea3d22bbe245e1184771b3e096');
#endif
#if !FileExists(PublishDir + "\es\PresentationUI.resources.dll")
RemovePriorRuntimeFile('es\PresentationUI.resources.dll', '2a7f552d84faa06edf0dcf6f3697af54495f87776659d1861ecfd40b8c5d9139');
#endif
#if !FileExists(PublishDir + "\es\ReachFramework.resources.dll")
RemovePriorRuntimeFile('es\ReachFramework.resources.dll', '3dc5a93f4a2bdf3da3b0fa74fa12bea89649b86247668faa9498b553f8fd6347');
#endif
#if !FileExists(PublishDir + "\es\System.Windows.Controls.Ribbon.resources.dll")
RemovePriorRuntimeFile('es\System.Windows.Controls.Ribbon.resources.dll', 'd8921d1267c0421b044f0cdea3a6a36f05126e97b806608a81e873ff1a1e0bcf');
#endif
#if !FileExists(PublishDir + "\es\System.Windows.Input.Manipulations.resources.dll")
RemovePriorRuntimeFile('es\System.Windows.Input.Manipulations.resources.dll', '5cd1c327bf31359e36fab90fde6a5a493498287628460dd0920f0061896d316a');
#endif
#if !FileExists(PublishDir + "\es\System.Xaml.resources.dll")
RemovePriorRuntimeFile('es\System.Xaml.resources.dll', '2307b3a47ab73ff2ef73292aedbb53fd320ca43c6eeef0d9a3a927c1b7e82391');
#endif
#if !FileExists(PublishDir + "\es\UIAutomationClient.resources.dll")
RemovePriorRuntimeFile('es\UIAutomationClient.resources.dll', 'd8a6889dcfc62aa5f0040d7f48e9add1c73538c3c4ae83780f4ab3e812f166db');
#endif
#if !FileExists(PublishDir + "\es\UIAutomationClientSideProviders.resources.dll")
RemovePriorRuntimeFile('es\UIAutomationClientSideProviders.resources.dll', '3cdfd9969252c8d8f2dc479d1e1a1827194bf845de921f29bf235dbc65476ac0');
#endif
#if !FileExists(PublishDir + "\es\UIAutomationProvider.resources.dll")
RemovePriorRuntimeFile('es\UIAutomationProvider.resources.dll', '53e852aa0bd921d16ed211e4f475d0388db25f420ad165b200625d9c6fe363b2');
#endif
#if !FileExists(PublishDir + "\es\UIAutomationTypes.resources.dll")
RemovePriorRuntimeFile('es\UIAutomationTypes.resources.dll', '737927fbed5b6190837389840f3821ab655fe2e29c6fc6425a3c7d3c91d8abc8');
#endif
#if !FileExists(PublishDir + "\es\WindowsBase.resources.dll")
RemovePriorRuntimeFile('es\WindowsBase.resources.dll', 'd41b8c1897dd7241f555e8ce0ff1feeebd46c681a1767fb2c84ce697daa7f0ca');
#endif
#if !FileExists(PublishDir + "\es\WindowsFormsIntegration.resources.dll")
RemovePriorRuntimeFile('es\WindowsFormsIntegration.resources.dll', '808ffed8f0c04825c1cc9ebcc8b474b6da70f5736e486419d64446e5dd2d48e4');
#endif
#if !FileExists(PublishDir + "\fr\PresentationCore.resources.dll")
RemovePriorRuntimeFile('fr\PresentationCore.resources.dll', '57a6af809ec0408c168217c3302f07091c9f387216574cf82fd7269f1a364c80');
#endif
#if !FileExists(PublishDir + "\fr\PresentationFramework.resources.dll")
RemovePriorRuntimeFile('fr\PresentationFramework.resources.dll', '6d2632a0e12a4e38f79e68e95998a838eebdf6e07cf6481964f8d326f1687535');
#endif
#if !FileExists(PublishDir + "\fr\PresentationUI.resources.dll")
RemovePriorRuntimeFile('fr\PresentationUI.resources.dll', '293e6508ad5cd7a9e97d75557b397d0e2eb8f362f3a7274ca86d8a9e45401624');
#endif
#if !FileExists(PublishDir + "\fr\ReachFramework.resources.dll")
RemovePriorRuntimeFile('fr\ReachFramework.resources.dll', '1d4f39f8afc84cf93026df40f79e115112a70f19f57c18d9922ec5367e9d2d98');
#endif
#if !FileExists(PublishDir + "\fr\System.Windows.Controls.Ribbon.resources.dll")
RemovePriorRuntimeFile('fr\System.Windows.Controls.Ribbon.resources.dll', '9f1097760799dcfcdd077f2d167e570a0a763c5791ca7ea799a5b9e51e2683ca');
#endif
#if !FileExists(PublishDir + "\fr\System.Windows.Input.Manipulations.resources.dll")
RemovePriorRuntimeFile('fr\System.Windows.Input.Manipulations.resources.dll', 'ab9213e1948ce40a3a54dcc4982ac102bee53ef9991e491b244117ab3e165630');
#endif
#if !FileExists(PublishDir + "\fr\System.Xaml.resources.dll")
RemovePriorRuntimeFile('fr\System.Xaml.resources.dll', '6a50698c7a79b8e6eea0df4e1b746fabf81fe07f1cd0d730cef5c23d00ceeafc');
#endif
#if !FileExists(PublishDir + "\fr\UIAutomationClient.resources.dll")
RemovePriorRuntimeFile('fr\UIAutomationClient.resources.dll', '11d6acf1766b4660f5d21120ac762cd89e68c158bd06533b079b397da5cf876f');
#endif
#if !FileExists(PublishDir + "\fr\UIAutomationClientSideProviders.resources.dll")
RemovePriorRuntimeFile('fr\UIAutomationClientSideProviders.resources.dll', 'ba6deb79b80ae725ee53f190f18e904ac9067c0822ee7e7732d96abebcf52065');
#endif
#if !FileExists(PublishDir + "\fr\UIAutomationProvider.resources.dll")
RemovePriorRuntimeFile('fr\UIAutomationProvider.resources.dll', '4e0725c0f9d0842e5e2a75e27c0e1a9de7d98d35f6517284b83b7affdb2a770a');
#endif
#if !FileExists(PublishDir + "\fr\UIAutomationTypes.resources.dll")
RemovePriorRuntimeFile('fr\UIAutomationTypes.resources.dll', 'f7d24ae4ca31fd1b185b260812065088e80c6d36d51a7edb1a9df2816d1d0f11');
#endif
#if !FileExists(PublishDir + "\fr\WindowsBase.resources.dll")
RemovePriorRuntimeFile('fr\WindowsBase.resources.dll', '1631c5f2573a4a3ee8c455d85b6b86691ad7106686970e5948b1abdf0edf1eb5');
#endif
#if !FileExists(PublishDir + "\fr\WindowsFormsIntegration.resources.dll")
RemovePriorRuntimeFile('fr\WindowsFormsIntegration.resources.dll', '5f47cc22ad7e122a73692e45d252fe274b7fc2f01f0a745964be162b0e5a9d48');
#endif
#if !FileExists(PublishDir + "\it\PresentationCore.resources.dll")
RemovePriorRuntimeFile('it\PresentationCore.resources.dll', 'e8575bd8aa1b321c87c4745b0a27964fb499afee5391f16498664bd5b3722c92');
#endif
#if !FileExists(PublishDir + "\it\PresentationFramework.resources.dll")
RemovePriorRuntimeFile('it\PresentationFramework.resources.dll', '3a85e85156dac3e79f0d70da38df5460ca8ddf53a336fc1dffcb52661563bda8');
#endif
#if !FileExists(PublishDir + "\it\PresentationUI.resources.dll")
RemovePriorRuntimeFile('it\PresentationUI.resources.dll', '473d27a08e02862696b50a01771400f0572212289b2ccb068c6cc39d0f2f5ec8');
#endif
#if !FileExists(PublishDir + "\it\ReachFramework.resources.dll")
RemovePriorRuntimeFile('it\ReachFramework.resources.dll', '3ee25f74889f9bba3d797cce328010ef193be30f4fb7cebdd89ee224b1762796');
#endif
#if !FileExists(PublishDir + "\it\System.Windows.Controls.Ribbon.resources.dll")
RemovePriorRuntimeFile('it\System.Windows.Controls.Ribbon.resources.dll', '0683324329724296fcdbeeb2a9b0548f942c89da71f8899ad7165c4890b56617');
#endif
#if !FileExists(PublishDir + "\it\System.Windows.Input.Manipulations.resources.dll")
RemovePriorRuntimeFile('it\System.Windows.Input.Manipulations.resources.dll', '14060b0d8a878784975a82f3405723ff223a376dbe17d29961c7db6d43d22476');
#endif
#if !FileExists(PublishDir + "\it\System.Xaml.resources.dll")
RemovePriorRuntimeFile('it\System.Xaml.resources.dll', '0a1cebc0c94f636b582bac046d0bd1dc832d11e59e4cc9505b806b9a5280490f');
#endif
#if !FileExists(PublishDir + "\it\UIAutomationClient.resources.dll")
RemovePriorRuntimeFile('it\UIAutomationClient.resources.dll', 'b045ff7f460b5c7169c1ffced6f597ca2c30a56c1adf063cf215c196707d5014');
#endif
#if !FileExists(PublishDir + "\it\UIAutomationClientSideProviders.resources.dll")
RemovePriorRuntimeFile('it\UIAutomationClientSideProviders.resources.dll', '4a3394285e02ac0e80c83349d7ce9a98d0b932fec82e5a6d1680436e17b18c96');
#endif
#if !FileExists(PublishDir + "\it\UIAutomationProvider.resources.dll")
RemovePriorRuntimeFile('it\UIAutomationProvider.resources.dll', '98b88fcc23295e6bce20d048f8b9385c02d8aa629412cbc21dcb92761088e6f1');
#endif
#if !FileExists(PublishDir + "\it\UIAutomationTypes.resources.dll")
RemovePriorRuntimeFile('it\UIAutomationTypes.resources.dll', 'e0ef299f80267b9dc7a2be732bee0af0decaae8407d88719efdc0640f0f0efd9');
#endif
#if !FileExists(PublishDir + "\it\WindowsBase.resources.dll")
RemovePriorRuntimeFile('it\WindowsBase.resources.dll', '6dfc6bf439c875f04aac430b13610c1c4d1822e42d12516d7547e1e73293cac8');
#endif
#if !FileExists(PublishDir + "\it\WindowsFormsIntegration.resources.dll")
RemovePriorRuntimeFile('it\WindowsFormsIntegration.resources.dll', 'b3f3207ca16bdebc0940fdbf32c728714ee99396918102beed5b774388a8c37b');
#endif
#if !FileExists(PublishDir + "\ja\PresentationCore.resources.dll")
RemovePriorRuntimeFile('ja\PresentationCore.resources.dll', '3eb03d240af577355cbd4dd32cab457fa0c70c9d6f9c2f4c8e9d0ecd5bc54f32');
#endif
#if !FileExists(PublishDir + "\ja\PresentationFramework.resources.dll")
RemovePriorRuntimeFile('ja\PresentationFramework.resources.dll', 'a3b4215f09be32ca9cd32c47cff9bca881849fdebfaf23841c27975df672daac');
#endif
#if !FileExists(PublishDir + "\ja\PresentationUI.resources.dll")
RemovePriorRuntimeFile('ja\PresentationUI.resources.dll', 'ac4322b20a6ac46ca77a04892a34771472253215b8ea0cb48010396ad711ae5e');
#endif
#if !FileExists(PublishDir + "\ja\ReachFramework.resources.dll")
RemovePriorRuntimeFile('ja\ReachFramework.resources.dll', 'd4f2bf06263ae0a8b5462f21862f0a55a849c54d013a12f1bb736cf98c7a10b7');
#endif
#if !FileExists(PublishDir + "\ja\System.Windows.Controls.Ribbon.resources.dll")
RemovePriorRuntimeFile('ja\System.Windows.Controls.Ribbon.resources.dll', '93d55dfb00bd747b87bd28b38e2a6599f0b029b29c95294fa6e9c4ded232b918');
#endif
#if !FileExists(PublishDir + "\ja\System.Windows.Input.Manipulations.resources.dll")
RemovePriorRuntimeFile('ja\System.Windows.Input.Manipulations.resources.dll', '9e4d68938fd4fd32b106fa55a84afa8bfb4f54bd45ed90266718a42c8f260fb7');
#endif
#if !FileExists(PublishDir + "\ja\System.Xaml.resources.dll")
RemovePriorRuntimeFile('ja\System.Xaml.resources.dll', 'faff153a5da97ecf7fdbbef53f720f231a8500074a869222f28cd833c009b3ea');
#endif
#if !FileExists(PublishDir + "\ja\UIAutomationClient.resources.dll")
RemovePriorRuntimeFile('ja\UIAutomationClient.resources.dll', '7606449d4001fa080f4374e2af0e20e75b5c89765cac74a005a2cec1cef090b4');
#endif
#if !FileExists(PublishDir + "\ja\UIAutomationClientSideProviders.resources.dll")
RemovePriorRuntimeFile('ja\UIAutomationClientSideProviders.resources.dll', 'fe41465963aa84c8192131ed756cfc0a023e51c50efb4988bc5e9f5897ae9d31');
#endif
#if !FileExists(PublishDir + "\ja\UIAutomationProvider.resources.dll")
RemovePriorRuntimeFile('ja\UIAutomationProvider.resources.dll', '15f905656ef0afdb62c9cde2ee27f050bbf8983a72363e1b0292a3963d02f990');
#endif
#if !FileExists(PublishDir + "\ja\UIAutomationTypes.resources.dll")
RemovePriorRuntimeFile('ja\UIAutomationTypes.resources.dll', 'c4f5459d08e86248caf45a442218de52518b4d5cc923c17f4fe4e50571577a8c');
#endif
#if !FileExists(PublishDir + "\ja\WindowsBase.resources.dll")
RemovePriorRuntimeFile('ja\WindowsBase.resources.dll', 'c9b8cb8ef305d9158f463a524b3efe24c0e439e601d26e88d532170e9939e74c');
#endif
#if !FileExists(PublishDir + "\ja\WindowsFormsIntegration.resources.dll")
RemovePriorRuntimeFile('ja\WindowsFormsIntegration.resources.dll', 'c9f9c8825bcd29027d8266b095ff62ac1038fded069676ea612d6ba762329501');
#endif
#if !FileExists(PublishDir + "\ko\PresentationCore.resources.dll")
RemovePriorRuntimeFile('ko\PresentationCore.resources.dll', '8cd1ec130df0476a0819e6a2d9b89cdc76c45a4251b959d34e4957ec48c723d2');
#endif
#if !FileExists(PublishDir + "\ko\PresentationFramework.resources.dll")
RemovePriorRuntimeFile('ko\PresentationFramework.resources.dll', 'f4a12b0ead4cad8b0e9503cfdc4a8711c4195442c194df30a9dc7af346e1bf51');
#endif
#if !FileExists(PublishDir + "\ko\PresentationUI.resources.dll")
RemovePriorRuntimeFile('ko\PresentationUI.resources.dll', 'a3fc57c3b22b201af920d74df88fbeb241937acf6cce27bb1678aa08117b4484');
#endif
#if !FileExists(PublishDir + "\ko\ReachFramework.resources.dll")
RemovePriorRuntimeFile('ko\ReachFramework.resources.dll', '0aa210742776e7306f264e11c1c6076dc3f73ce050da48325bda336a09a98e42');
#endif
#if !FileExists(PublishDir + "\ko\System.Windows.Controls.Ribbon.resources.dll")
RemovePriorRuntimeFile('ko\System.Windows.Controls.Ribbon.resources.dll', '51c119a9e83765f610a3249b0d27046f194590ad98d0f660d8f8784609d5e0d7');
#endif
#if !FileExists(PublishDir + "\ko\System.Windows.Input.Manipulations.resources.dll")
RemovePriorRuntimeFile('ko\System.Windows.Input.Manipulations.resources.dll', 'f2b7a1a99ce5ce3a240f4ba9d6a741a2212349d40d5272d3f12f36f695a33394');
#endif
#if !FileExists(PublishDir + "\ko\System.Xaml.resources.dll")
RemovePriorRuntimeFile('ko\System.Xaml.resources.dll', 'f47d86aca6c28fc750f99b6868961952ce89974529d5892cf9b67a3879873f75');
#endif
#if !FileExists(PublishDir + "\ko\UIAutomationClient.resources.dll")
RemovePriorRuntimeFile('ko\UIAutomationClient.resources.dll', '4be04f28211a5751e33a983bd2fff2484ab68c1bb416df409da2714e8c01a53f');
#endif
#if !FileExists(PublishDir + "\ko\UIAutomationClientSideProviders.resources.dll")
RemovePriorRuntimeFile('ko\UIAutomationClientSideProviders.resources.dll', '5f246a2f75f218c1efdbb19b8ed4f1ffbe84fe6e986fffc8aaf53c06e551b195');
#endif
#if !FileExists(PublishDir + "\ko\UIAutomationProvider.resources.dll")
RemovePriorRuntimeFile('ko\UIAutomationProvider.resources.dll', '5c011d579b4612cb514eb56d94a1e5e6694a9c28356f173978d99688aedc6400');
#endif
#if !FileExists(PublishDir + "\ko\UIAutomationTypes.resources.dll")
RemovePriorRuntimeFile('ko\UIAutomationTypes.resources.dll', '661058b09d98948bdf2d95f498d469be0f7aa2c3e5402175066b5a92ff487c2c');
#endif
#if !FileExists(PublishDir + "\ko\WindowsBase.resources.dll")
RemovePriorRuntimeFile('ko\WindowsBase.resources.dll', 'e23fce24e1d50bd2934626bd8fddea0649c0865947215edf4c55b262284f1353');
#endif
#if !FileExists(PublishDir + "\ko\WindowsFormsIntegration.resources.dll")
RemovePriorRuntimeFile('ko\WindowsFormsIntegration.resources.dll', '9354f55ec60b34a255d806be644fb2255e8b6066e83becaee968dcd8097c3f3b');
#endif
#if !FileExists(PublishDir + "\mscordaccore_amd64_amd64_8.0.3326.52219.dll")
RemovePriorRuntimeFile('mscordaccore_amd64_amd64_8.0.3326.52219.dll', '40e1279137ce03acf937d8f18f587cb1aa08aa1482d8e4098dce72554e44ef22');
#endif
#if !FileExists(PublishDir + "\PenImc_cor3.dll")
RemovePriorRuntimeFile('PenImc_cor3.dll', 'b1ea770f4ddc92be8b4e78f782ba6a88b58d62f2e70a4d458ae7cf3b70f887c5');
#endif
#if !FileExists(PublishDir + "\pl\PresentationCore.resources.dll")
RemovePriorRuntimeFile('pl\PresentationCore.resources.dll', '71e4b1f375a9863a2da4e28ea238f800b09834764fd625a2022ffd245add1030');
#endif
#if !FileExists(PublishDir + "\pl\PresentationFramework.resources.dll")
RemovePriorRuntimeFile('pl\PresentationFramework.resources.dll', 'f8fc9c0b91b9434964cb3356c05c54534cb5cffe7e1f9a1df2468d2368c02338');
#endif
#if !FileExists(PublishDir + "\pl\PresentationUI.resources.dll")
RemovePriorRuntimeFile('pl\PresentationUI.resources.dll', '9413cdefb0fbd9ddd6c1043114e78ef288e7e664e56216ee745d634bf39a6b1b');
#endif
#if !FileExists(PublishDir + "\pl\ReachFramework.resources.dll")
RemovePriorRuntimeFile('pl\ReachFramework.resources.dll', '15179f25326b5f8d50b27a618343dfcebbe471af7bafc2c9f3985f43f7dd88df');
#endif
#if !FileExists(PublishDir + "\pl\System.Windows.Controls.Ribbon.resources.dll")
RemovePriorRuntimeFile('pl\System.Windows.Controls.Ribbon.resources.dll', '367fe1ebedac7033d5ce50204d6d13c73c7e223f4685baf12e08b83a5bdf7c4f');
#endif
#if !FileExists(PublishDir + "\pl\System.Windows.Input.Manipulations.resources.dll")
RemovePriorRuntimeFile('pl\System.Windows.Input.Manipulations.resources.dll', '163b72918bb544d32e53444fa7acdc378ca2e27191a6ce11821b0b09f99bac8c');
#endif
#if !FileExists(PublishDir + "\pl\System.Xaml.resources.dll")
RemovePriorRuntimeFile('pl\System.Xaml.resources.dll', '1764427bab43469fca8e5faffc87454d5a4b48a135c155f63a0a2ca9331a6bd8');
#endif
#if !FileExists(PublishDir + "\pl\UIAutomationClient.resources.dll")
RemovePriorRuntimeFile('pl\UIAutomationClient.resources.dll', '62639cc2ef82309b27fa4241a140c335014e5497fdcc3e7a46865a04b601a68a');
#endif
#if !FileExists(PublishDir + "\pl\UIAutomationClientSideProviders.resources.dll")
RemovePriorRuntimeFile('pl\UIAutomationClientSideProviders.resources.dll', '369897f563f1ef30a316b840db248657063fa46d9a1105298d2ccd7743f9e1ea');
#endif
#if !FileExists(PublishDir + "\pl\UIAutomationProvider.resources.dll")
RemovePriorRuntimeFile('pl\UIAutomationProvider.resources.dll', 'b73eeb1d517e0d13430e2debc7912fdcf7d6052e945872748ddee37b908315b5');
#endif
#if !FileExists(PublishDir + "\pl\UIAutomationTypes.resources.dll")
RemovePriorRuntimeFile('pl\UIAutomationTypes.resources.dll', '61e314316d92d58c84073ebb9d2c03cab957d1fdf74cc6b73b7bfbf5dae86f88');
#endif
#if !FileExists(PublishDir + "\pl\WindowsBase.resources.dll")
RemovePriorRuntimeFile('pl\WindowsBase.resources.dll', 'ff37f1cba32e5a9e218c028d353cecbc05a5af8a15059bb479db41905b63d32d');
#endif
#if !FileExists(PublishDir + "\pl\WindowsFormsIntegration.resources.dll")
RemovePriorRuntimeFile('pl\WindowsFormsIntegration.resources.dll', '879c48cfa0076c99802a5d62627fdb8ce0bd65c2c175459eae39d248387b6873');
#endif
#if !FileExists(PublishDir + "\PresentationCore.dll")
RemovePriorRuntimeFile('PresentationCore.dll', 'ee5131513fdef511bb00acd20836b41426f0bf6b5c556b31b7999ce143c9c52e');
#endif
#if !FileExists(PublishDir + "\PresentationFramework-SystemCore.dll")
RemovePriorRuntimeFile('PresentationFramework-SystemCore.dll', '833271e875f53b07285b30c2152c27c9c01439ebfd8e2f41e7d67f50f8df8efc');
#endif
#if !FileExists(PublishDir + "\PresentationFramework-SystemData.dll")
RemovePriorRuntimeFile('PresentationFramework-SystemData.dll', '832bdcd6b8fb3e4f1c7515634d3aa74f4f8b376ba3b8d2b0624c2cac3a7184a9');
#endif
#if !FileExists(PublishDir + "\PresentationFramework-SystemDrawing.dll")
RemovePriorRuntimeFile('PresentationFramework-SystemDrawing.dll', '7c175e21b2d65eff66ded21f6669ef4ac2a53d6311382e614fe689a657163bc4');
#endif
#if !FileExists(PublishDir + "\PresentationFramework-SystemXml.dll")
RemovePriorRuntimeFile('PresentationFramework-SystemXml.dll', 'e7a9c1fe1298d47eaad273ca49c140751a3f1dc6385d0c239cd428b57a9287e6');
#endif
#if !FileExists(PublishDir + "\PresentationFramework-SystemXmlLinq.dll")
RemovePriorRuntimeFile('PresentationFramework-SystemXmlLinq.dll', 'e8f10e5dadb6bee1c9fc72849980ab2bd0dd0e5bf8ba192a1028354aab0946f3');
#endif
#if !FileExists(PublishDir + "\PresentationFramework.Aero.dll")
RemovePriorRuntimeFile('PresentationFramework.Aero.dll', 'd193b629507d002c8c0f0da27c04f606aa3dc0d25dd4c7f5469bae7002a2ba2a');
#endif
#if !FileExists(PublishDir + "\PresentationFramework.Aero2.dll")
RemovePriorRuntimeFile('PresentationFramework.Aero2.dll', '33449c997282878446c56f141f70b11ec45670220a5f27cdd3be8d1c11157c7a');
#endif
#if !FileExists(PublishDir + "\PresentationFramework.AeroLite.dll")
RemovePriorRuntimeFile('PresentationFramework.AeroLite.dll', '8851a78e0fc36ab6b0fa55070890d0feb1cbf1b6a2ca3f537ac2cbe1aab45e11');
#endif
#if !FileExists(PublishDir + "\PresentationFramework.Classic.dll")
RemovePriorRuntimeFile('PresentationFramework.Classic.dll', 'c224cf087da9cb7df32c1dccb1a19abccc78478e592881851fee1a88445eebe9');
#endif
#if !FileExists(PublishDir + "\PresentationFramework.dll")
RemovePriorRuntimeFile('PresentationFramework.dll', '4be294f6cf23b15c2d2ab3adbe683d10a514924d5fe90c4a4c415657b77bc6d3');
#endif
#if !FileExists(PublishDir + "\PresentationFramework.Luna.dll")
RemovePriorRuntimeFile('PresentationFramework.Luna.dll', '85e7cb9c5d3eadc5cad17390b2258fd59cc305112d5b7780144ec5d2afaf3611');
#endif
#if !FileExists(PublishDir + "\PresentationFramework.Royale.dll")
RemovePriorRuntimeFile('PresentationFramework.Royale.dll', 'fe51d9b6bab7e5ab367e6cab80d1f207b9d93b2146d73bfd98590427d0f6aeb9');
#endif
#if !FileExists(PublishDir + "\PresentationNative_cor3.dll")
RemovePriorRuntimeFile('PresentationNative_cor3.dll', 'c9de9598d1ae7d7b9cfd3e83c7201bf2ad23b97121000c475de01a868ac2e618');
#endif
#if !FileExists(PublishDir + "\PresentationUI.dll")
RemovePriorRuntimeFile('PresentationUI.dll', 'd12978044bfb0ef54b94fdefb0b6724b8a5c880fa31e7131e38c2b68dbb949c7');
#endif
#if !FileExists(PublishDir + "\pt-BR\PresentationCore.resources.dll")
RemovePriorRuntimeFile('pt-BR\PresentationCore.resources.dll', 'ccc7844ffa840ada7293099d749fc348481e0f53bb8bba461a58ea4f4848169f');
#endif
#if !FileExists(PublishDir + "\pt-BR\PresentationFramework.resources.dll")
RemovePriorRuntimeFile('pt-BR\PresentationFramework.resources.dll', '68633f6e24e869b88466618829ae433dbea66404de49afc9e68b5b94d956b989');
#endif
#if !FileExists(PublishDir + "\pt-BR\PresentationUI.resources.dll")
RemovePriorRuntimeFile('pt-BR\PresentationUI.resources.dll', '856d904c785e5206fa1378031578dd50ec72aa2520323252d79fdea3eea31edf');
#endif
#if !FileExists(PublishDir + "\pt-BR\ReachFramework.resources.dll")
RemovePriorRuntimeFile('pt-BR\ReachFramework.resources.dll', '4fdb3c62319436f9191bebccbfe87179e1b58ebe73faa773c36da86351b570d4');
#endif
#if !FileExists(PublishDir + "\pt-BR\System.Windows.Controls.Ribbon.resources.dll")
RemovePriorRuntimeFile('pt-BR\System.Windows.Controls.Ribbon.resources.dll', 'dfec8132eaac28d924dc759a81f73cefa942dfb1621d7dfb2ef983865e0f7e1f');
#endif
#if !FileExists(PublishDir + "\pt-BR\System.Windows.Input.Manipulations.resources.dll")
RemovePriorRuntimeFile('pt-BR\System.Windows.Input.Manipulations.resources.dll', '17eca5f21319b702feccbda8744397b37392cefde5edc4d11888630b37a09514');
#endif
#if !FileExists(PublishDir + "\pt-BR\System.Xaml.resources.dll")
RemovePriorRuntimeFile('pt-BR\System.Xaml.resources.dll', 'b5047f53e1ad4fb6a6ac9a53cdbfc46d20a6e1adb1c5c9ba86ee6f62f3c4b5dd');
#endif
#if !FileExists(PublishDir + "\pt-BR\UIAutomationClient.resources.dll")
RemovePriorRuntimeFile('pt-BR\UIAutomationClient.resources.dll', '565997d0aabd63d736addae9399a858548262d473963f16fb9dd2759b366f696');
#endif
#if !FileExists(PublishDir + "\pt-BR\UIAutomationClientSideProviders.resources.dll")
RemovePriorRuntimeFile('pt-BR\UIAutomationClientSideProviders.resources.dll', '52b80430dabc84dd0e2227b64b938004b029370e48d7e2d5f4be3ce928025784');
#endif
#if !FileExists(PublishDir + "\pt-BR\UIAutomationProvider.resources.dll")
RemovePriorRuntimeFile('pt-BR\UIAutomationProvider.resources.dll', '3743edd5cf27b87893de94db1ac5431dab92473a8f302b890ac14ad3c946c430');
#endif
#if !FileExists(PublishDir + "\pt-BR\UIAutomationTypes.resources.dll")
RemovePriorRuntimeFile('pt-BR\UIAutomationTypes.resources.dll', 'd4e6293fa36b0eb61b7b09a85747448bd301cff1e2fd2a19989f8304a1741799');
#endif
#if !FileExists(PublishDir + "\pt-BR\WindowsBase.resources.dll")
RemovePriorRuntimeFile('pt-BR\WindowsBase.resources.dll', '6856163213d102b6103893008b2a5d2519a4e441d0dc710f29e565e7597f4113');
#endif
#if !FileExists(PublishDir + "\pt-BR\WindowsFormsIntegration.resources.dll")
RemovePriorRuntimeFile('pt-BR\WindowsFormsIntegration.resources.dll', '63659059c394d0a91e4da03eaae40b7284e80e8d5c5e69d6ee4f8a98fb19cd60');
#endif
#if !FileExists(PublishDir + "\ReachFramework.dll")
RemovePriorRuntimeFile('ReachFramework.dll', '53d394fd0d75d47c02b860206272acf83a94d408ba161cbcf29ac12bdbf4640c');
#endif
#if !FileExists(PublishDir + "\ru\PresentationCore.resources.dll")
RemovePriorRuntimeFile('ru\PresentationCore.resources.dll', '97d7d29e469f54dc74f11c2da17709bda117db6ab7c8d8d0f72f0998c34528c1');
#endif
#if !FileExists(PublishDir + "\ru\PresentationFramework.resources.dll")
RemovePriorRuntimeFile('ru\PresentationFramework.resources.dll', '1ca4adfbea7e9340e81f085d463f085c1aa42a09b3ed216e0e9855c32e3ece0a');
#endif
#if !FileExists(PublishDir + "\ru\PresentationUI.resources.dll")
RemovePriorRuntimeFile('ru\PresentationUI.resources.dll', '08c7ac675054712a0a5e145de11415ed4adb91cb28c0bf81881ece50f9a20add');
#endif
#if !FileExists(PublishDir + "\ru\ReachFramework.resources.dll")
RemovePriorRuntimeFile('ru\ReachFramework.resources.dll', 'e8e3beaf0bad1b3164ff9444d3a7edc3c7acb712ddd507211711e89b7177b7c4');
#endif
#if !FileExists(PublishDir + "\ru\System.Windows.Controls.Ribbon.resources.dll")
RemovePriorRuntimeFile('ru\System.Windows.Controls.Ribbon.resources.dll', '76059b957ded304f8f020d6c3ef41b65d5a76f84c4ff957676226edcc87d0da4');
#endif
#if !FileExists(PublishDir + "\ru\System.Windows.Input.Manipulations.resources.dll")
RemovePriorRuntimeFile('ru\System.Windows.Input.Manipulations.resources.dll', 'd84daeda51647a9f6ee22571c0bf1f4d043e745164e83e4b41f5a45af86dc911');
#endif
#if !FileExists(PublishDir + "\ru\System.Xaml.resources.dll")
RemovePriorRuntimeFile('ru\System.Xaml.resources.dll', '0148c10b3ebf5db3b0480477feb0549b66ba46205fbd0dcc4639b313182b8ec6');
#endif
#if !FileExists(PublishDir + "\ru\UIAutomationClient.resources.dll")
RemovePriorRuntimeFile('ru\UIAutomationClient.resources.dll', 'b19e14dc127d58cf32cb7ad96795defc232bab96d256a7a5cc3304c684071303');
#endif
#if !FileExists(PublishDir + "\ru\UIAutomationClientSideProviders.resources.dll")
RemovePriorRuntimeFile('ru\UIAutomationClientSideProviders.resources.dll', '1601a2163876d083681b4602081c8244f12e50dace182eb124468a0b050f38c3');
#endif
#if !FileExists(PublishDir + "\ru\UIAutomationProvider.resources.dll")
RemovePriorRuntimeFile('ru\UIAutomationProvider.resources.dll', 'fed3bf6923d0e84bddb4e1b045533eb69bc255d5978e6ac62d2d5956cabea670');
#endif
#if !FileExists(PublishDir + "\ru\UIAutomationTypes.resources.dll")
RemovePriorRuntimeFile('ru\UIAutomationTypes.resources.dll', '6e59a21a58e8646f6d70c13627f69fc6ea942039c35f9e899602679f8b9e3327');
#endif
#if !FileExists(PublishDir + "\ru\WindowsBase.resources.dll")
RemovePriorRuntimeFile('ru\WindowsBase.resources.dll', '08aff71030445d269e9adffc4f107bbeecee2c3b15355892d48d5a8e7190bed4');
#endif
#if !FileExists(PublishDir + "\ru\WindowsFormsIntegration.resources.dll")
RemovePriorRuntimeFile('ru\WindowsFormsIntegration.resources.dll', 'ec727874ae4e0efffa5c7c36173d607ddde7f8805857120fe2532ac47f2ce4de');
#endif
#if !FileExists(PublishDir + "\System.Printing.dll")
RemovePriorRuntimeFile('System.Printing.dll', '74b379217bfd966379a7c2ce339719fa5863c69e33f06dd011a6a6824f945abd');
#endif
#if !FileExists(PublishDir + "\System.Windows.Controls.Ribbon.dll")
RemovePriorRuntimeFile('System.Windows.Controls.Ribbon.dll', 'c98837837b01e421ad1daccd43633cf93f2719ac0ebc25b1f6d82244a91720ca');
#endif
#if !FileExists(PublishDir + "\System.Windows.Input.Manipulations.dll")
RemovePriorRuntimeFile('System.Windows.Input.Manipulations.dll', '97c2da71c087364aedfc6de93041811016233d1dd101fa9bd94bdf80419284d1');
#endif
#if !FileExists(PublishDir + "\System.Windows.Presentation.dll")
RemovePriorRuntimeFile('System.Windows.Presentation.dll', '1b099d53515b7c03fcecf06e65eec5c93d20faa9c9a7b038eae9c1c99357dd3f');
#endif
#if !FileExists(PublishDir + "\System.Xaml.dll")
RemovePriorRuntimeFile('System.Xaml.dll', '902dc15cdf41712f1c9486ff412c61cecd6eccd8b0e508e9727c5107da7a0230');
#endif
#if !FileExists(PublishDir + "\tr\PresentationCore.resources.dll")
RemovePriorRuntimeFile('tr\PresentationCore.resources.dll', '563c5c7c351ef806ccb937ddb4e960e988f3f00ceabd4d3368b489473db8e41e');
#endif
#if !FileExists(PublishDir + "\tr\PresentationFramework.resources.dll")
RemovePriorRuntimeFile('tr\PresentationFramework.resources.dll', '43c495f6168b183879b5afb0efe57f8b5acfb279c5923063d4d97308c5e36538');
#endif
#if !FileExists(PublishDir + "\tr\PresentationUI.resources.dll")
RemovePriorRuntimeFile('tr\PresentationUI.resources.dll', '43b22251d4e1c69906aef05a2ba561dd5dc3a43a063612a80fd826909e1c3e36');
#endif
#if !FileExists(PublishDir + "\tr\ReachFramework.resources.dll")
RemovePriorRuntimeFile('tr\ReachFramework.resources.dll', 'b3122f94b17ebc43232a4963a651db869bbc77373a5b1247cc8e33afdf23aed0');
#endif
#if !FileExists(PublishDir + "\tr\System.Windows.Controls.Ribbon.resources.dll")
RemovePriorRuntimeFile('tr\System.Windows.Controls.Ribbon.resources.dll', '2e34f5ea196f26c2de4394ca9c1b5c6507d60856848480a83cd41ad18bbc0ab3');
#endif
#if !FileExists(PublishDir + "\tr\System.Windows.Input.Manipulations.resources.dll")
RemovePriorRuntimeFile('tr\System.Windows.Input.Manipulations.resources.dll', 'a3190400bd30b4c954339773a88240839fa144c921f409558cbff807a8a5cc4e');
#endif
#if !FileExists(PublishDir + "\tr\System.Xaml.resources.dll")
RemovePriorRuntimeFile('tr\System.Xaml.resources.dll', '0aff3799b1048be2823081a0df7c260192dda777531c134ce7721992769bebcc');
#endif
#if !FileExists(PublishDir + "\tr\UIAutomationClient.resources.dll")
RemovePriorRuntimeFile('tr\UIAutomationClient.resources.dll', '55fb68e8013aca0cc7f4018dbc3cc2b9925e3868a773fddd7c861aa81d2307b9');
#endif
#if !FileExists(PublishDir + "\tr\UIAutomationClientSideProviders.resources.dll")
RemovePriorRuntimeFile('tr\UIAutomationClientSideProviders.resources.dll', 'ed0c30ba551990624a3cbe442980591a0d28870e3fae9eeeb875dd78f059914e');
#endif
#if !FileExists(PublishDir + "\tr\UIAutomationProvider.resources.dll")
RemovePriorRuntimeFile('tr\UIAutomationProvider.resources.dll', '63ffb921c219b0eca77c0e5755c8cf7bc8a39efead0c9834fccb88075428b38b');
#endif
#if !FileExists(PublishDir + "\tr\UIAutomationTypes.resources.dll")
RemovePriorRuntimeFile('tr\UIAutomationTypes.resources.dll', '5819bfb1846a8426d516a61216a55df9431a2d369f19ae323fd8ece87e0a4c2c');
#endif
#if !FileExists(PublishDir + "\tr\WindowsBase.resources.dll")
RemovePriorRuntimeFile('tr\WindowsBase.resources.dll', '771c8e2fef83ed443a6e02159d16a77ec4018e5ae5056cf188bad6918fd85f67');
#endif
#if !FileExists(PublishDir + "\tr\WindowsFormsIntegration.resources.dll")
RemovePriorRuntimeFile('tr\WindowsFormsIntegration.resources.dll', '4ccb6a3c0cd4caa3ae4fe205c9701b0c5c1f40c3d8b407babc1975ab660f81b5');
#endif
#if !FileExists(PublishDir + "\UIAutomationClient.dll")
RemovePriorRuntimeFile('UIAutomationClient.dll', '852de571c78c4b46690dd1d02c1025280e0bc2f45538f011c63160507ef6c9ba');
#endif
#if !FileExists(PublishDir + "\UIAutomationClientSideProviders.dll")
RemovePriorRuntimeFile('UIAutomationClientSideProviders.dll', 'fd5d9592c7964db26d66f70a20caf8b5135b7c287b80d1fafc56ac56e23ebdcd');
#endif
#if !FileExists(PublishDir + "\UIAutomationProvider.dll")
RemovePriorRuntimeFile('UIAutomationProvider.dll', '13412d171fa596bd37b93500119860647637e353cdc00bbd68ffbe781b91ac5a');
#endif
#if !FileExists(PublishDir + "\UIAutomationTypes.dll")
RemovePriorRuntimeFile('UIAutomationTypes.dll', 'd9bef49a643fce8ebe3528a3244f41a085f50b29ed0b56900e081125ee561379');
#endif
#if !FileExists(PublishDir + "\vcruntime140_cor3.dll")
RemovePriorRuntimeFile('vcruntime140_cor3.dll', 'd5e4d9a3e835fa679450145d6a7d94e36573a509317111904d9b3712c30d9066');
#endif
#if !FileExists(PublishDir + "\WindowsFormsIntegration.dll")
RemovePriorRuntimeFile('WindowsFormsIntegration.dll', 'c15f2af3ef488d6c72f1c0abdca7af03511889dc7f43a3fff383ee2d0ac9fce8');
#endif
#if !FileExists(PublishDir + "\wpfgfx_cor3.dll")
RemovePriorRuntimeFile('wpfgfx_cor3.dll', 'dda5747c0b09a8d9ffa3db5da7f9a80b3c0ee39bb42c145fffdb93a337c1dff0');
#endif
#if !FileExists(PublishDir + "\zh-Hans\PresentationCore.resources.dll")
RemovePriorRuntimeFile('zh-Hans\PresentationCore.resources.dll', '84486a51fddc2fdb051222f5432da878a61a3c6527b5a04514ff3c9d8140a838');
#endif
#if !FileExists(PublishDir + "\zh-Hans\PresentationFramework.resources.dll")
RemovePriorRuntimeFile('zh-Hans\PresentationFramework.resources.dll', '51e50cab68ed1b89018abda9b77e2f1a11afdb697aad8a29a025a5e53c87ba22');
#endif
#if !FileExists(PublishDir + "\zh-Hans\PresentationUI.resources.dll")
RemovePriorRuntimeFile('zh-Hans\PresentationUI.resources.dll', '71fd1ddfc37a47e3cac5f174baf1ec67fd1254cf02bbc5ecf19565e590442d5c');
#endif
#if !FileExists(PublishDir + "\zh-Hans\ReachFramework.resources.dll")
RemovePriorRuntimeFile('zh-Hans\ReachFramework.resources.dll', '0a1366fa2fa6f596d8e64983ef35994dd4f4668a5fd7b30eec3fa69039627f24');
#endif
#if !FileExists(PublishDir + "\zh-Hans\System.Windows.Controls.Ribbon.resources.dll")
RemovePriorRuntimeFile('zh-Hans\System.Windows.Controls.Ribbon.resources.dll', 'f32d6527222f103b2f52982e9bd6786cfe5edece2f50615f5dee1250cc0907c1');
#endif
#if !FileExists(PublishDir + "\zh-Hans\System.Windows.Input.Manipulations.resources.dll")
RemovePriorRuntimeFile('zh-Hans\System.Windows.Input.Manipulations.resources.dll', '7ae9d19e40e585b1d909757812c20f8abf2b71a4f5c8e43799893c222e77a006');
#endif
#if !FileExists(PublishDir + "\zh-Hans\System.Xaml.resources.dll")
RemovePriorRuntimeFile('zh-Hans\System.Xaml.resources.dll', 'acae4c8ac7a7d28a635110f1821a47d78a746eb0658b8a4dc7b85393b73c84f1');
#endif
#if !FileExists(PublishDir + "\zh-Hans\UIAutomationClient.resources.dll")
RemovePriorRuntimeFile('zh-Hans\UIAutomationClient.resources.dll', '960530a695bffe2c6679a883a40cc3fa134a8a269c4c4059f03131f318520e63');
#endif
#if !FileExists(PublishDir + "\zh-Hans\UIAutomationClientSideProviders.resources.dll")
RemovePriorRuntimeFile('zh-Hans\UIAutomationClientSideProviders.resources.dll', '5b60fa5e991620ea63a97ce266862e3bf3fdaa1493ee538b7f2779ac6455a043');
#endif
#if !FileExists(PublishDir + "\zh-Hans\UIAutomationProvider.resources.dll")
RemovePriorRuntimeFile('zh-Hans\UIAutomationProvider.resources.dll', '7c452e7a5b7091632bff0d0e425b46d0aa79e10c9e79db139d5e12b12c034dbb');
#endif
#if !FileExists(PublishDir + "\zh-Hans\UIAutomationTypes.resources.dll")
RemovePriorRuntimeFile('zh-Hans\UIAutomationTypes.resources.dll', '9f1652061ed373a06ab43fd9973568ef84346c306f2090032ca47e9f7fe960c1');
#endif
#if !FileExists(PublishDir + "\zh-Hans\WindowsBase.resources.dll")
RemovePriorRuntimeFile('zh-Hans\WindowsBase.resources.dll', '11cf1fd6e7e4831d138c973f908a14cf72c73b28675f41672dc32fc519ad322c');
#endif
#if !FileExists(PublishDir + "\zh-Hans\WindowsFormsIntegration.resources.dll")
RemovePriorRuntimeFile('zh-Hans\WindowsFormsIntegration.resources.dll', '2222695499953688c37e5d393106cbb9ac9510ce001fbe21e4d98fbb76f4e86a');
#endif
#if !FileExists(PublishDir + "\zh-Hant\PresentationCore.resources.dll")
RemovePriorRuntimeFile('zh-Hant\PresentationCore.resources.dll', 'ec720fb654bc0ae5cdc77b951f65ed4dc6dc5bceb6db6114d987cd91d55662ce');
#endif
#if !FileExists(PublishDir + "\zh-Hant\PresentationFramework.resources.dll")
RemovePriorRuntimeFile('zh-Hant\PresentationFramework.resources.dll', 'd9cf11d4494765cdd4908bc21577498691715d85b8087d747bd8cdf9c2431a30');
#endif
#if !FileExists(PublishDir + "\zh-Hant\PresentationUI.resources.dll")
RemovePriorRuntimeFile('zh-Hant\PresentationUI.resources.dll', 'e5b97249b515049adb91f49534c46222c28f201b99af634647dbed57857411ec');
#endif
#if !FileExists(PublishDir + "\zh-Hant\ReachFramework.resources.dll")
RemovePriorRuntimeFile('zh-Hant\ReachFramework.resources.dll', 'a180685ee912dc64f768ff0c500c20b8baeb7826f329b9cb9df320eb19dcecdc');
#endif
#if !FileExists(PublishDir + "\zh-Hant\System.Windows.Controls.Ribbon.resources.dll")
RemovePriorRuntimeFile('zh-Hant\System.Windows.Controls.Ribbon.resources.dll', '1e993f1ea9a469120479c24a4ed5c872830c8f998fc4165a18dbbe02a416d813');
#endif
#if !FileExists(PublishDir + "\zh-Hant\System.Windows.Input.Manipulations.resources.dll")
RemovePriorRuntimeFile('zh-Hant\System.Windows.Input.Manipulations.resources.dll', '21d0c883fbbb9a2f6e359f301087446e4292ad20ad7fdf3a2b6ec2a7c9c621cb');
#endif
#if !FileExists(PublishDir + "\zh-Hant\System.Xaml.resources.dll")
RemovePriorRuntimeFile('zh-Hant\System.Xaml.resources.dll', '0538a900c949db90824bad6ba49c4cb8b3c081d2f53520aab95a1a1830faea2c');
#endif
#if !FileExists(PublishDir + "\zh-Hant\UIAutomationClient.resources.dll")
RemovePriorRuntimeFile('zh-Hant\UIAutomationClient.resources.dll', '5f1bcf7ab37314fce60bae71fcee62985028b4384136eddedad0a114638d686c');
#endif
#if !FileExists(PublishDir + "\zh-Hant\UIAutomationClientSideProviders.resources.dll")
RemovePriorRuntimeFile('zh-Hant\UIAutomationClientSideProviders.resources.dll', 'aa023029920c521b4f2046b9eb81b872006eac402c7b56c270a114eba08ea5f2');
#endif
#if !FileExists(PublishDir + "\zh-Hant\UIAutomationProvider.resources.dll")
RemovePriorRuntimeFile('zh-Hant\UIAutomationProvider.resources.dll', '505ab3a41d5c0adc4c38e64d19d1d9fd9495f63cd97c5ab70cbc236934db1ccd');
#endif
#if !FileExists(PublishDir + "\zh-Hant\UIAutomationTypes.resources.dll")
RemovePriorRuntimeFile('zh-Hant\UIAutomationTypes.resources.dll', '1abbf4dc17b87b07bbb291b6801bf3f6b476eb391a7d9585afc8ebb759d6fc32');
#endif
#if !FileExists(PublishDir + "\zh-Hant\WindowsBase.resources.dll")
RemovePriorRuntimeFile('zh-Hant\WindowsBase.resources.dll', '4cf347ac3d445a895103b8c7e59cd0bbad1ea0f53fb00cf282fe72ded10f8e58');
#endif
#if !FileExists(PublishDir + "\zh-Hant\WindowsFormsIntegration.resources.dll")
RemovePriorRuntimeFile('zh-Hant\WindowsFormsIntegration.resources.dll', '97205f7000af6d0d49d385fbbc78ef971d673597bf38bf3f2c9ef5390dee4c56');
#endif
