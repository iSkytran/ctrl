# Celeste Reinforcement Learning

This repository contains a pipeline to run a reinforcement learning agent in Celeste. This was done as a final project for an independent study at North Carolina State University (CSC 498). The corresponding write-up can be found in [final_paper_public.pdf](final_paper_public.pdf).

The [server](server) folder contains the mod files needed for Celeste to communicate with the reinforcement learning agent. The [client](client) folder contains the code needed to run the agent itself.

In order to use the client, the package must be installed using pip first. The server mod must be loaded through [Everest](https://everestapi.github.io/) and requires copying the additional dependency DLL files into the Celeste mod folder as well.
