import {parentPort, threadId} from "node:worker_threads";

parentPort?.on('message',async (message) => {
    if(message?.delay) {
        await new Promise((resolve, reject) => {
          setTimeout(resolve, message.delay);
        })
    }
    if(message?.error) {
        parentPort.postMessage(new Error(message.error));
        return;
    }
    parentPort.postMessage({message});
}).on('close', () => {
    console.debug(`Closed: ${threadId}`);
});
