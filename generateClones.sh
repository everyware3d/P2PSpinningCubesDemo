# call from parent directory (e.g., inside UnityProjects/P2PUnityDemo where P2PUnityDemo1 is from repo)
cp -r P2PUnityDemo1 P2PUnityDemo2
cp -r P2PUnityDemo1 P2PUnityDemo3
cp -r P2PUnityDemo1 P2PUnityDemo4
cp -r P2PUnityDemo1 P2PUnityDemo5
rm -rf P2PUnityDemo2/Assets
rm -rf P2PUnityDemo3/Assets
rm -rf P2PUnityDemo4/Assets
rm -rf P2PUnityDemo5/Assets
cd P2PUnityDemo2; ln -s ../P2PUnityDemo1/Assets Assets; cd ..
cd P2PUnityDemo3; ln -s ../P2PUnityDemo1/Assets Assets; cd ..
cd P2PUnityDemo4; ln -s ../P2PUnityDemo1/Assets Assets; cd ..
cd P2PUnityDemo5; ln -s ../P2PUnityDemo1/Assets Assets; cd ..
