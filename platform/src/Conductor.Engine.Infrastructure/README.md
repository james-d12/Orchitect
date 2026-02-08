# Conductor Infrastructure
This project contains code relating to third party dependencies. It currently contains the 'drivers' like Terraform and Helm Chart. 
These drivers are the ones responsible for applying resource templates. Conductor works under the assumption that you will 
be integrating it with your already existing Infrastructure As Code (IaC) solutions that you have setup. This way you can point 
Conductor to your existing IaC without having to do any importing.