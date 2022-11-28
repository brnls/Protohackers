$serviceName = "protohackers.service"
sudo systemctl stop $serviceName
sudo rm ../service-user/$serviceName -r
tar -xzvf Publish.tar.gz; 
sudo mv PublishOutput ../service-user/$serviceName
sudo systemctl disable $serviceName
sudo rm /etc/systemd/system/$serviceName
sudo mv $serviceName /etc/systemd/system/$serviceName
sudo systemctl daemon-reload
sudo systemctl enable $serviceName
sudo systemctl start $serviceName
sudo rm Publish.tar.gz