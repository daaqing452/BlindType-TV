package com.example.eftei;

import java.io.OutputStreamWriter;
import java.io.PrintWriter;
import java.net.Socket;
import java.util.ArrayList;
import java.util.Timer;
import java.util.TimerTask;

import com.example.eftei.R;

import android.app.Activity;
import android.os.AsyncTask;
import android.os.Bundle;
import android.util.DisplayMetrics;
import android.view.MotionEvent;
import android.view.View;
import android.view.View.OnClickListener;
import android.widget.Button;
import android.widget.EditText;
import android.widget.TextView;

public class MainActivity extends Activity {

	static int connectPort = 10309;

	int width;
	int height;
	
	Socket socket = null;
	Button clearButton, okButton;
	EditText ipEditText;
	PrintWriter printWriter;
	TextView infoTextView;
	
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		setContentView(R.layout.activity_main);
		infoTextView = (TextView)findViewById(R.id.infoTextView);
		okButton = (Button)findViewById(R.id.okButton);
		okButton.setOnClickListener(okButtonOnClickListener);
		ipEditText = (EditText)findViewById(R.id.ipEditText);
		
		DisplayMetrics displayMetrics = new DisplayMetrics();
		getWindowManager().getDefaultDisplay().getMetrics(displayMetrics);
		width = displayMetrics.widthPixels;
		height = displayMetrics.heightPixels;
	}
	
	OnClickListener okButtonOnClickListener = new OnClickListener() {
		public void onClick(View v) {
			if (socket == null) {
				new NetworkAsyncTask().execute(ipEditText.getText().toString());
			} else {
				socket = null;
				infoTextView.setText("Hello");
			}
		}
	};
	
	final int HOLDDOWN_TIME = 500;
	final int HOLDDOWN_DIST = 60;
	final int SLIP_DIST = 80;
	
	final int MODE_NONE = 0;
	final int MODE_CLICK = 1;
	final int MODE_DRAG = 2;
	
	int n;
	ArrayList<OnePointTouch> opta = new ArrayList<OnePointTouch>();
	int moveX = 0, moveY = 0;
	int mode = 0;
	
	public boolean onTouchEvent(MotionEvent event) {
		
		if (socket == null) return super.onTouchEvent(event);
		n = event.getActionIndex();
		int x = (int)event.getX();
		int y = (int)event.getY();
		
		printWriter.write(n + " " + event.getAction() + " (" + event.getX(n) + " " + event.getY(n) + ")\n");
		printWriter.flush();
		
		switch (event.getAction() & MotionEvent.ACTION_MASK) {
		
		case MotionEvent.ACTION_DOWN:
		case MotionEvent.ACTION_POINTER_DOWN:
			opta.add(new OnePointTouch(x, y));
			mode = MODE_CLICK;
			break;
			
		case MotionEvent.ACTION_MOVE:
			switch (mode) {
			case MODE_CLICK:
				moveX = x;
				moveY = y;
				break;
			case MODE_DRAG:
				printWriter.write("drag " + x + " " + y + "\n");
				printWriter.flush();
				break;
			}
			break;
		
		case MotionEvent.ACTION_UP:
		case MotionEvent.ACTION_POINTER_UP:
			OnePointTouch opt = opta.get(n);
			switch (mode) {
			case MODE_CLICK:
				//	point
				if (Math.abs(opt.downX - x) < HOLDDOWN_DIST && Math.abs(opt.downY - y) < HOLDDOWN_DIST) {
					int midx = (x + opt.downX) / 2;
					int midy = (y + opt.downY) / 2;
					printWriter.write("addpoint " + midx + " " + midy + "\n");
					printWriter.flush();
				}
				//	left flick
				if (x < opt.downX - SLIP_DIST) {
					printWriter.write("backspace\n");
					printWriter.flush();
				}
				//	right flick
				if (x > opt.downX + SLIP_DIST) {
					printWriter.write("space\n");
					printWriter.flush();
				}
				break;
			case MODE_DRAG:
				//	drag
				printWriter.write("dragend " + x + " " + y + "\n");
				printWriter.flush();
				break;
			}
			mode = MODE_NONE;
			opt.cancel();
			opta.remove(n);
			break;
		}
		
		return super.onTouchEvent(event);
	}
	
	class OnePointTouch {
		public int downX, downY;
		public Timer dragTimer;
		
		public OnePointTouch(int _downX, int _downY) {
			downX = _downX;
			downY = _downY;
			dragTimer = new Timer();
			dragTimer.schedule(new TimerTask() {
				public void run() {
					if (Math.abs(moveX - downX) < HOLDDOWN_DIST && Math.abs(moveY - downY) < HOLDDOWN_DIST) {
						mode = MODE_DRAG;
						printWriter.write("dragbegin " + moveX + " " + moveY + "\n");
						printWriter.flush();
					}
				}
			}, HOLDDOWN_TIME);
		}
		
		public void cancel() {
			dragTimer.cancel();
		}
	}
	
	class NetworkAsyncTask extends AsyncTask<String, Integer, String> {
		
		protected String doInBackground(String... params) {
			try {
				socket = new Socket(params[0], connectPort);
				printWriter = new PrintWriter(new OutputStreamWriter(socket.getOutputStream()));
				Thread.sleep(300);
				printWriter.write("devicesize " + width + " " + height + "\n");
				printWriter.flush();
				return socket.toString();
			} catch (Exception e) {
				socket = null;
				return e.toString();
			}
		}
		
		protected void onPostExecute(String string) {
			infoTextView.setText(string);
		}
	}
}
