declare module 'stream' {
  export class Stream {}
}

declare module 'events' {
  export class EventEmitter {}
}

declare namespace NodeJS {
  interface TypedArray extends ArrayBufferView {}
}
